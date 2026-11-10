using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SimMetryx.TelemetrySniff.Core.AssettoCorsa;
using SimMetryx.TelemetrySniff.Core.Models;
using SimMetryx.TelemetrySniff.Core.Storage;

namespace SimMetryx.TelemetrySniff.Cli
{
    internal class Program
    {
        private static bool _running = true;
        private static long _packetCount = 0;
        private static long _lastPacketCount = 0;
        private static double _currentHz = 0;
        private static DateTime _lastHzCalc = DateTime.UtcNow;

        private static AcHandshakeResponse? _handshake;
        private static AcCarInfo? _latestCarInfo;
        private static TelemetryPacket? _latestPacket;
        private static string _statusMessage = "Initializing...";

        static async Task Main(string[] args)
        {
            Console.Title = "SimMetryx Telemetry Observer - Assetto Corsa UDP Listener";
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string mode = "active"; // active, passive, demo
            string ip = "127.0.0.1";
            int port = 9996;
            string? outDir = null;
            bool record = true;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLowerInvariant();
                if (arg == "--mode" && i + 1 < args.Length) mode = args[++i].ToLowerInvariant();
                else if (arg == "--ip" && i + 1 < args.Length) ip = args[++i];
                else if (arg == "--port" && i + 1 < args.Length && int.TryParse(args[++i], out var p)) port = p;
                else if (arg == "--out" && i + 1 < args.Length) outDir = args[++i];
                else if (arg == "--no-record") record = false;
                else if (arg == "--help" || arg == "-h")
                {
                    PrintHelp();
                    return;
                }
            }

            TelemetrySessionWriter? writer = null;
            if (record)
            {
                writer = new TelemetrySessionWriter(outDir);
                writer.Start("AssettoCorsa");
            }

            Console.Clear();
            Console.CursorVisible = false;

            var cts = new CancellationTokenSource();

            // Run appropriate mode
            Task? workerTask = null;
            AssettoCorsaUdpClient? acClient = null;
            PassiveUdpSniffer? passiveSniffer = null;

            if (mode == "demo")
            {
                _statusMessage = "Running in Simulated Demo Mode";
                _handshake = new AcHandshakeResponse
                {
                    CarName = "ks_ferrari_488_gt3",
                    DriverName = "Sim Driver",
                    TrackName = "monza",
                    TrackConfig = "gp"
                };
                workerTask = Task.Run(() => RunDemoLoop(writer, cts.Token));
            }
            else if (mode == "passive")
            {
                _statusMessage = $"Passive Sniffing on UDP port {port}";
                passiveSniffer = new PassiveUdpSniffer(port);
                passiveSniffer.OnAcCarInfoParsed += (car, pkt) =>
                {
                    Interlocked.Increment(ref _packetCount);
                    _latestCarInfo = car;
                    _latestPacket = pkt;
                    writer?.Enqueue(pkt);
                };
                passiveSniffer.OnLogMessage += msg => _statusMessage = msg;
                passiveSniffer.OnError += ex => _statusMessage = $"[Error] {ex.Message}";
                passiveSniffer.Start();
            }
            else // "active"
            {
                _statusMessage = $"Listening for Assetto Corsa at {ip}:{port}";
                acClient = new AssettoCorsaUdpClient(ip, port);
                acClient.OnHandshakeCompleted += hs =>
                {
                    _handshake = hs;
                    _statusMessage = $"Connected: {hs.CarName} @ {hs.TrackName}";
                };
                acClient.OnTelemetryReceived += (car, pkt) =>
                {
                    Interlocked.Increment(ref _packetCount);
                    _latestCarInfo = car;
                    _latestPacket = pkt;
                    writer?.Enqueue(pkt);
                };
                acClient.OnLapReceived += lap =>
                {
                    _statusMessage = $"Lap Completed: Lap {lap.Lap} - {TimeSpan.FromMilliseconds(lap.LapTimeMs):mm\\:ss\\.fff}";
                };
                acClient.OnLogMessage += msg => _statusMessage = msg;
                acClient.OnError += ex => _statusMessage = $"[Error] {ex.Message}";
                workerTask = acClient.StartAsync();
            }

            // Keyboard handler loop
            var keyTask = Task.Run(() =>
            {
                while (_running)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.Q || key == ConsoleKey.Escape)
                        {
                            _running = false;
                            cts.Cancel();
                            break;
                        }
                        if (key == ConsoleKey.H && acClient != null)
                        {
                            _ = acClient.SendHandshakeAsync();
                            _statusMessage = "Manual Handshake triggered.";
                        }
                    }
                    Thread.Sleep(50);
                }
            });

            // HUD refresh loop
            try
            {
                while (_running)
                {
                    CalculateHz();
                    RenderHud(mode, ip, port, writer);
                    await Task.Delay(100);
                }
            }
            finally
            {
                Console.CursorVisible = true;
                _statusMessage = "Shutting down sniffer...";
                cts.Cancel();
                acClient?.Dispose();
                passiveSniffer?.Dispose();
                if (writer != null)
                {
                    writer.Stop(_latestPacket);
                    Console.WriteLine();
                    Console.WriteLine($"[Storage] Telemetry stream saved: {writer.CurrentSessionFile}");
                    Console.WriteLine($"[Storage] Schema catalog saved:  {writer.CurrentCatalogFile}");
                }
            }
        }

        private static void CalculateHz()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastHzCalc).TotalSeconds;
            if (elapsed >= 1.0)
            {
                var delta = _packetCount - _lastPacketCount;
                _currentHz = delta / elapsed;
                _lastPacketCount = _packetCount;
                _lastHzCalc = now;
            }
        }

        private static void RenderHud(string mode, string ip, int port, TelemetrySessionWriter? writer)
        {
            try
            {
                Console.SetCursorPosition(0, 0);
            }
            catch
            {
                // In non-interactive terminals, avoid throwing
            }

            var line = new string('=', 78);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(line);
            Console.WriteLine(" 🏎️  SIMMETRYX TELEMETRY OBSERVER  |  Assetto Corsa UDP Engine");
            Console.WriteLine(line);
            Console.ResetColor();

            Console.WriteLine($" Mode: {mode.ToUpperInvariant()} | Target: {ip}:{port} | Rate: {_currentHz,5:F1} Hz | Packets: {_packetCount,8:N0}");
            Console.WriteLine($" Status: {_statusMessage,-68}");

            var car = _handshake?.CarName ?? (_latestCarInfo != null ? "Assetto Corsa Car" : "Waiting for sim...");
            var track = _handshake?.TrackName ?? (_latestCarInfo != null ? "Active Track" : "Waiting for track...");
            var driver = _handshake?.DriverName ?? "Waiting for driver...";

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($" Vehicle: {car,-28} Track: {track,-20} Driver: {driver}");
            Console.ResetColor();

            Console.WriteLine(new string('-', 78));

            if (_latestCarInfo != null)
            {
                var speedKmh = _latestCarInfo.SpeedKmh;
                var speedMph = _latestCarInfo.SpeedMph;
                var gear = FormatGear(_latestCarInfo.Gear);
                var rpm = _latestCarInfo.EngineRpm;
                var gas = _latestCarInfo.Gas;
                var brake = _latestCarInfo.Brake;
                var clutch = _latestCarInfo.Clutch;
                var steer = _latestCarInfo.Steer;

                Console.WriteLine($" SPEED: {speedKmh,5:F1} km/h ({speedMph,5:F1} mph)   GEAR: [{gear}]   RPM: {rpm,5:F0}");
                Console.WriteLine($" GAS:    {MakeBar(gas, 20)} {gas * 100,5:F1}%");
                Console.WriteLine($" BRAKE:  {MakeBar(brake, 20)} {brake * 100,5:F1}%");
                Console.WriteLine($" CLUTCH: {MakeBar(clutch, 20)} {clutch * 100,5:F1}%");
                Console.WriteLine($" STEER:  {steer,6:F2} rad | G-Forces: Long { _latestCarInfo.AccGFrontal,5:F2}G  Lat {_latestCarInfo.AccGHorizontal,5:F2}G  Vert {_latestCarInfo.AccGVertical,5:F2}G");

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" 🛞  WHEEL TELEMETRY (FL, FR, RL, RR):");
                Console.ResetColor();
                Console.WriteLine($"  Slip Ratio:  [{_latestCarInfo.SlipRatio[0],5:F2}, {_latestCarInfo.SlipRatio[1],5:F2}, {_latestCarInfo.SlipRatio[2],5:F2}, {_latestCarInfo.SlipRatio[3],5:F2}]");
                Console.WriteLine($"  Slip Angle:  [{_latestCarInfo.SlipAngle[0],5:F2}, {_latestCarInfo.SlipAngle[1],5:F2}, {_latestCarInfo.SlipAngle[2],5:F2}, {_latestCarInfo.SlipAngle[3],5:F2}]");
                Console.WriteLine($"  Suspension:  [{_latestCarInfo.SuspensionHeight[0],5:F1}, {_latestCarInfo.SuspensionHeight[1],5:F1}, {_latestCarInfo.SuspensionHeight[2],5:F1}, {_latestCarInfo.SuspensionHeight[3],5:F1}] mm");

                Console.WriteLine();
                var curLap = _latestCarInfo.LapTimeMs > 0 ? TimeSpan.FromMilliseconds(_latestCarInfo.LapTimeMs).ToString(@"mm\:ss\.fff") : "--:--.---";
                var bestLap = _latestCarInfo.BestLapMs > 0 ? TimeSpan.FromMilliseconds(_latestCarInfo.BestLapMs).ToString(@"mm\:ss\.fff") : "--:--.---";
                var lastLap = _latestCarInfo.LastLapMs > 0 ? TimeSpan.FromMilliseconds(_latestCarInfo.LastLapMs).ToString(@"mm\:ss\.fff") : "--:--.---";
                Console.WriteLine($" LAP: {_latestCarInfo.LapCount,2} | Current: {curLap} | Last: {lastLap} | Best: {bestLap}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(" [No live telemetry stream detected yet]");
                Console.WriteLine(" Tip: In Assetto Corsa, ensure remote telemetry or UDP broadcast is active on port 9996.");
                Console.WriteLine(" Tip: Press [H] to re-send handshake request, or run with '--mode demo' to test HUD.");
                Console.ResetColor();
                for (int i = 0; i < 9; i++) Console.WriteLine(new string(' ', 78));
            }

            Console.WriteLine(new string('-', 78));
            if (writer != null)
            {
                Console.WriteLine($" 💾 Recording: {Path.GetFileName(writer.CurrentSessionFile)} ({writer.FramesWritten:N0} frames)");
            }
            else
            {
                Console.WriteLine(" 💾 Recording: Disabled (--no-record)");
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(" Hotkeys: [Q / Esc] Quit  |  [H] Re-send Handshake");
            Console.ResetColor();
        }

        private static string MakeBar(float value, int totalWidth)
        {
            var clamped = Math.Max(0.0f, Math.Min(1.0f, value));
            var filled = (int)(clamped * totalWidth);
            return $"[{new string('█', filled)}{new string('-', totalWidth - filled)}]";
        }

        private static string FormatGear(int rawGear)
        {
            if (rawGear <= 0) return "R";
            if (rawGear == 1) return "N";
            return (rawGear - 1).ToString();
        }

        private static async Task RunDemoLoop(TelemetrySessionWriter? writer, CancellationToken token)
        {
            float speed = 0f;
            float rpm = 1000f;
            int gear = 2;
            float gas = 0.8f;
            int lapTime = 0;

            while (!token.IsCancellationRequested)
            {
                speed += 1.2f;
                if (speed > 240f) speed = 60f;

                rpm += 150f;
                if (rpm > 8500f)
                {
                    rpm = 4000f;
                    gear = Math.Min(7, gear + 1);
                }

                lapTime += 33;

                var car = new AcCarInfo
                {
                    Identifier = 'a',
                    SpeedKmh = speed,
                    SpeedMph = speed * 0.621371f,
                    EngineRpm = rpm,
                    Gear = gear,
                    Gas = gas,
                    Brake = 0.05f,
                    Clutch = 0f,
                    Steer = (float)Math.Sin(lapTime / 1000.0) * 0.2f,
                    AccGFrontal = 0.45f,
                    AccGHorizontal = 0.82f,
                    AccGVertical = 0.05f,
                    LapTimeMs = lapTime,
                    LapCount = 3,
                    SlipRatio = new[] { 0.02f, 0.02f, 0.04f, 0.04f },
                    SlipAngle = new[] { 0.08f, 0.09f, 0.03f, 0.03f },
                    SuspensionHeight = new[] { 54.2f, 54.5f, 62.1f, 61.8f }
                };

                var pkt = new TelemetryPacket
                {
                    TimestampUtc = DateTime.UtcNow,
                    CarModel = "ks_ferrari_488_gt3",
                    DriverName = "Sim Driver",
                    TrackCode = "monza",
                    Normalized = new NormalizedData
                    {
                        SpeedKmh = speed,
                        Rpms = rpm,
                        Gear = (gear - 1).ToString(),
                        Throttle = gas,
                        Brake = 0.05,
                        SteeringAngle = car.Steer,
                        CompletedLaps = 3,
                        CurrentLapTime = TimeSpan.FromMilliseconds(lapTime)
                    },
                    RawPayload = car
                };

                Interlocked.Increment(ref _packetCount);
                _latestCarInfo = car;
                _latestPacket = pkt;
                writer?.Enqueue(pkt);

                await Task.Delay(33, token); // ~30 Hz
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"
SimMetryx Telemetry Observer - Assetto Corsa UDP Engine

Usage:
  SimMetryx.TelemetrySniff.Cli [options]

Options:
  --mode <active|passive|demo>  Operation mode:
                                active:  Initiates UDP handshake to Assetto Corsa and subscribes (Default)
                                passive: Passively sniffs incoming broadcast UDP packets on port
                                demo:    Simulates live telemetry data to preview HUD and output formats
  --ip <ip_address>             Target simulator host IP (Default: 127.0.0.1)
  --port <port_number>          UDP telemetry port (Default: 9996)
  --out <directory_path>        Custom directory for output .jsonl and .catalog.json files
  --no-record                   Disable saving capture frames to disk
  --help, -h                    Show this help screen
");
        }
    }
}
