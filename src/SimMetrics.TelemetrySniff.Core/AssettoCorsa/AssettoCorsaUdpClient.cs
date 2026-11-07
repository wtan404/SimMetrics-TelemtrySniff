using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SimMetrics.TelemetrySniff.Core.Models;

namespace SimMetrics.TelemetrySniff.Core.AssettoCorsa
{
    /// <summary>
    /// Active UDP client communicating with Assetto Corsa Remote Telemetry server.
    /// Manages handshakes, subscription updates, and packet parsing.
    /// </summary>
    public class AssettoCorsaUdpClient : IDisposable
    {
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;
        private Task? _keepAliveTask;

        public string HostIp { get; }
        public int Port { get; }
        public bool IsRunning { get; private set; }
        public AcHandshakeResponse? CurrentSession { get; private set; }

        public event Action<AcHandshakeResponse>? OnHandshakeCompleted;
        public event Action<AcCarInfo, TelemetryPacket>? OnTelemetryReceived;
        public event Action<AcLapInfo>? OnLapReceived;
        public event Action<string>? OnLogMessage;
        public event Action<Exception>? OnError;

        public AssettoCorsaUdpClient(string hostIp = "127.0.0.1", int port = 9996)
        {
            HostIp = hostIp;
            Port = port;
        }

        public async Task StartAsync()
        {
            if (IsRunning) return;

            _udpClient = new UdpClient();
            _udpClient.Connect(HostIp, Port);
            _cts = new CancellationTokenSource();
            IsRunning = true;

            OnLogMessage?.Invoke($"[AC UDP Client] Connecting to Assetto Corsa on {HostIp}:{Port}...");

            // Send initial handshake
            await SendHandshakeAsync();

            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            _keepAliveTask = Task.Run(() => KeepAliveLoopAsync(_cts.Token));
        }

        public async Task SendHandshakeAsync()
        {
            if (_udpClient == null) return;
            try
            {
                var packet = BuildCommandPacket(1, 1, (int)AcOperationId.Handshake);
                await _udpClient.SendAsync(packet, packet.Length);
                OnLogMessage?.Invoke("[AC UDP Client] Handshake datagram sent.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        public async Task SubscribeUpdateAsync()
        {
            if (_udpClient == null) return;
            try
            {
                var packet = BuildCommandPacket(1, 1, (int)AcOperationId.SubscribeUpdate);
                await _udpClient.SendAsync(packet, packet.Length);
                OnLogMessage?.Invoke("[AC UDP Client] SubscribeUpdate datagram sent.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        public async Task SubscribeSpotAsync()
        {
            if (_udpClient == null) return;
            try
            {
                var packet = BuildCommandPacket(1, 1, (int)AcOperationId.SubscribeSpot);
                await _udpClient.SendAsync(packet, packet.Length);
                OnLogMessage?.Invoke("[AC UDP Client] SubscribeSpot datagram sent.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        public async Task DismissAsync()
        {
            if (_udpClient == null) return;
            try
            {
                var packet = BuildCommandPacket(1, 1, (int)AcOperationId.Dismiss);
                await _udpClient.SendAsync(packet, packet.Length);
                OnLogMessage?.Invoke("[AC UDP Client] Dismiss datagram sent.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _udpClient != null)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    var bytes = result.Buffer;

                    if (bytes.Length == 0) continue;

                    // Check for Handshake Response (408 bytes)
                    if (bytes.Length == 408)
                    {
                        var hs = AcHandshakeResponse.Parse(bytes);
                        if (hs != null)
                        {
                            CurrentSession = hs;
                            OnHandshakeCompleted?.Invoke(hs);
                            OnLogMessage?.Invoke($"[AC UDP Client] Handshake confirmed: Car={hs.CarName}, Driver={hs.DriverName}, Track={hs.TrackName}");
                            // Immediately subscribe to live telemetry stream
                            await SubscribeUpdateAsync();
                            await SubscribeSpotAsync();
                            continue;
                        }
                    }

                    char id = (char)bytes[0];
                    if (id == 'a' || bytes.Length >= 328) // RT_CAR_INFO
                    {
                        var carInfo = AcCarInfo.Parse(bytes);
                        if (carInfo != null)
                        {
                            var packet = NormalizeAcData(carInfo, CurrentSession);
                            OnTelemetryReceived?.Invoke(carInfo, packet);
                        }
                    }
                    else if (id == 'l' || bytes.Length >= 220) // RT_LAP
                    {
                        var lapInfo = AcLapInfo.Parse(bytes);
                        if (lapInfo != null)
                        {
                            OnLapReceived?.Invoke(lapInfo);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException sex) when (sex.SocketErrorCode == SocketError.Interrupted || token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                }
            }
        }

        private async Task KeepAliveLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, token);
                    // Refresh subscription every 5 seconds to keep AC broadcasting active
                    if (IsRunning && CurrentSession != null)
                    {
                        await SubscribeUpdateAsync();
                    }
                    else if (IsRunning && CurrentSession == null)
                    {
                        // Retry handshake if not yet established
                        await SendHandshakeAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                }
            }
        }

        private static byte[] BuildCommandPacket(int identifier, int version, int operationId)
        {
            var buffer = new byte[12];
            BitConverter.GetBytes(identifier).CopyTo(buffer, 0);
            BitConverter.GetBytes(version).CopyTo(buffer, 4);
            BitConverter.GetBytes(operationId).CopyTo(buffer, 8);
            return buffer;
        }

        private static TelemetryPacket NormalizeAcData(AcCarInfo car, AcHandshakeResponse? session)
        {
            var packet = new TelemetryPacket
            {
                TimestampUtc = DateTime.UtcNow,
                GameName = "AssettoCorsa",
                GameRunning = true,
                CarModel = session?.CarName ?? "Assetto Corsa Vehicle",
                DriverName = session?.DriverName ?? "Driver",
                TrackCode = session?.TrackName ?? "Track",
                TrackConfig = session?.TrackConfig,
                Normalized = new NormalizedData
                {
                    SpeedKmh = car.SpeedKmh,
                    Rpms = car.EngineRpm,
                    MaxRpm = car.EngineRpm > 0 ? Math.Max(car.EngineRpm, 8000) : 8000,
                    Gear = FormatGear(car.Gear),
                    Throttle = car.Gas,
                    Brake = car.Brake,
                    Clutch = car.Clutch,
                    SteeringAngle = car.Steer,
                    AccelerationSurge = car.AccGFrontal,
                    AccelerationSway = car.AccGHorizontal,
                    AccelerationHeave = car.AccGVertical,
                    CompletedLaps = car.LapCount,
                    CurrentLapTime = car.LapTimeMs > 0 ? TimeSpan.FromMilliseconds(car.LapTimeMs) : null,
                    LastLapTime = car.LastLapMs > 0 ? TimeSpan.FromMilliseconds(car.LastLapMs) : null,
                    BestLapTime = car.BestLapMs > 0 ? TimeSpan.FromMilliseconds(car.BestLapMs) : null,
                    TrackPositionPercent = car.CarPositionNormalized * 100.0,
                    AbsInAction = car.IsAbsInAction,
                    TcInAction = car.IsTcInAction,
                    InPit = car.IsInPit,
                    EngineLimiterOn = car.IsEngineLimiterOn,
                    Tyres = new TyreData
                    {
                        SuspensionTravelMm = new[]
                        {
                            (double)car.SuspensionHeight[0],
                            (double)car.SuspensionHeight[1],
                            (double)car.SuspensionHeight[2],
                            (double)car.SuspensionHeight[3]
                        },
                        SlipAngle = new[]
                        {
                            (double)car.SlipAngle[0],
                            (double)car.SlipAngle[1],
                            (double)car.SlipAngle[2],
                            (double)car.SlipAngle[3]
                        },
                        SlipRatio = new[]
                        {
                            (double)car.SlipRatio[0],
                            (double)car.SlipRatio[1],
                            (double)car.SlipRatio[2],
                            (double)car.SlipRatio[3]
                        }
                    }
                },
                RawPayload = car
            };

            return packet;
        }

        private static string FormatGear(int rawGear)
        {
            // In AC UDP: 0 = Reverse, 1 = Neutral, 2 = 1st, 3 = 2nd, etc.
            if (rawGear <= 0) return "R";
            if (rawGear == 1) return "N";
            return (rawGear - 1).ToString();
        }

        public void Dispose()
        {
            if (!IsRunning) return;
            IsRunning = false;

            try
            {
                DismissAsync().Wait(1000);
            }
            catch { /* ignore during shutdown */ }

            _cts?.Cancel();
            _udpClient?.Close();
            _udpClient?.Dispose();
            _cts?.Dispose();
        }
    }
}
