using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SimMetryx.TelemetrySniff.Core.Models;

namespace SimMetryx.TelemetrySniff.Core.AssettoCorsa
{
    /// <summary>
    /// Passive UDP socket listener that binds to a port and intercepts
    /// incoming telemetry broadcasts without sending handshake packets.
    /// </summary>
    public class PassiveUdpSniffer : IDisposable
    {
        private UdpClient? _listener;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;

        public int ListenPort { get; }
        public bool IsListening { get; private set; }
        public long PacketsReceived { get; private set; }
        public long BytesReceived { get; private set; }

        public event Action<byte[], IPEndPoint>? OnRawDatagramReceived;
        public event Action<AcCarInfo, TelemetryPacket>? OnAcCarInfoParsed;
        public event Action<string>? OnLogMessage;
        public event Action<Exception>? OnError;

        public PassiveUdpSniffer(int listenPort = 9996)
        {
            ListenPort = listenPort;
        }

        public void Start()
        {
            if (IsListening) return;

            try
            {
                _listener = new UdpClient(new IPEndPoint(IPAddress.Any, ListenPort));
                _listener.Client.ReceiveBufferSize = 1024 * 1024; // 1MB buffer
                _cts = new CancellationTokenSource();
                IsListening = true;

                OnLogMessage?.Invoke($"[Passive UDP Sniffer] Bound to 0.0.0.0:{ListenPort}. Awaiting broadcast packets...");

                _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                throw;
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var result = await _listener.ReceiveAsync();
                    var buffer = result.Buffer;
                    PacketsReceived++;
                    BytesReceived += buffer.Length;

                    OnRawDatagramReceived?.Invoke(buffer, result.RemoteEndPoint);

                    // Attempt AC RT_CAR_INFO decode if signature matches
                    if (buffer.Length >= 100 && (char)buffer[0] == 'a')
                    {
                        var carInfo = AcCarInfo.Parse(buffer);
                        if (carInfo != null)
                        {
                            var packet = new TelemetryPacket
                            {
                                TimestampUtc = DateTime.UtcNow,
                                GameName = "AssettoCorsa",
                                Normalized = new NormalizedData
                                {
                                    SpeedKmh = carInfo.SpeedKmh,
                                    Rpms = carInfo.EngineRpm,
                                    Gear = carInfo.Gear <= 0 ? "R" : (carInfo.Gear == 1 ? "N" : (carInfo.Gear - 1).ToString()),
                                    Throttle = carInfo.Gas,
                                    Brake = carInfo.Brake,
                                    Clutch = carInfo.Clutch,
                                    SteeringAngle = carInfo.Steer,
                                    AccelerationSurge = carInfo.AccGFrontal,
                                    AccelerationSway = carInfo.AccGHorizontal,
                                    AccelerationHeave = carInfo.AccGVertical,
                                    CompletedLaps = carInfo.LapCount,
                                    CurrentLapTime = carInfo.LapTimeMs > 0 ? TimeSpan.FromMilliseconds(carInfo.LapTimeMs) : null,
                                    TrackPositionPercent = carInfo.CarPositionNormalized * 100.0
                                },
                                RawPayload = carInfo
                            };
                            OnAcCarInfoParsed?.Invoke(carInfo, packet);
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

        public void Stop()
        {
            if (!IsListening) return;
            IsListening = false;

            _cts?.Cancel();
            _listener?.Close();
            _listener?.Dispose();
            _cts?.Dispose();

            OnLogMessage?.Invoke("[Passive UDP Sniffer] Stopped listening.");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
