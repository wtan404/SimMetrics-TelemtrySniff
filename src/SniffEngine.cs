using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GameReaderCommon;
using Newtonsoft.Json;
using SimHub.Plugins;
using SimMetrics.TelemetrySniff.Models;

namespace SimMetrics.TelemetrySniff
{
    /// <summary>
    /// Telemetry capture engine that samples normalized SimHub data, game raw buffers,
    /// and dynamic property catalogs without blocking the main game telemetry thread.
    /// </summary>
    public class SniffEngine : IDisposable
    {
        private readonly BlockingCollection<TelemetryPacket> _packetQueue = new BlockingCollection<TelemetryPacket>(5000);
        private CancellationTokenSource? _cts;
        private Task? _writerTask;
        private StreamWriter? _currentWriter;
        private DateTime _lastSampleTime = DateTime.MinValue;

        // Statistics
        public long FramesCaptured { get; private set; }
        public double CurrentHz { get; private set; }
        public int DiscoveredPropertyCount { get; private set; }
        public string? CurrentSessionFile { get; private set; }

        // Settings
        public bool IsCapturing { get; private set; }
        public int TargetHz { get; set; } = 30; // 1 to 60 Hz
        public bool CaptureRawPayload { get; set; } = true;
        public bool CaptureAllProperties { get; set; } = true;
        public string OutputDirectory { get; set; }

        public SniffEngine()
        {
            // Default output directory in Documents\SimMetrics
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            OutputDirectory = Path.Combine(docs, "SimMetrics", "TelemetrySniff");
        }

        public void StartCapture(string gameName)
        {
            if (IsCapturing) return;

            Directory.CreateDirectory(OutputDirectory);
            var safeGame = string.IsNullOrWhiteSpace(gameName) ? "UnknownSim" : SanitizeFilename(gameName);
            var filename = $"sniff_{safeGame}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jsonl";
            CurrentSessionFile = Path.Combine(OutputDirectory, filename);

            _currentWriter = new StreamWriter(new FileStream(CurrentSessionFile, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = false
            };

            _cts = new CancellationTokenSource();
            _writerTask = Task.Run(() => ProcessWriteQueue(_cts.Token));
            FramesCaptured = 0;
            IsCapturing = true;
        }

        public void StopCapture(PluginManager pluginManager)
        {
            if (!IsCapturing) return;

            IsCapturing = false;
            _cts?.Cancel();

            try
            {
                _writerTask?.Wait(2000);
            }
            catch (Exception) { /* ignored */ }

            _currentWriter?.Flush();
            _currentWriter?.Dispose();
            _currentWriter = null;

            // Generate schema catalog alongside the recording
            if (CurrentSessionFile != null)
            {
                ExportSchemaCatalog(pluginManager, CurrentSessionFile);
            }
        }

        /// <summary>
        /// Invoked every tick from SimHub's DataUpdate hook.
        /// </summary>
        public void ProcessFrame(PluginManager pluginManager, ref GameData data)
        {
            if (!IsCapturing || data?.NewData == null) return;

            var now = DateTime.UtcNow;
            var intervalMs = TargetHz > 0 ? (1000.0 / TargetHz) : 0;
            if ((now - _lastSampleTime).TotalMilliseconds < intervalMs)
            {
                return;
            }
            _lastSampleTime = now;

            var packet = new TelemetryPacket
            {
                TimestampUtc = now,
                GameName = pluginManager.GameName ?? "Unknown",
                GameRunning = data.GameRunning,
                GamePaused = data.GamePaused,
                SessionType = data.SessionTypeName,
                CarModel = data.NewData.CarModel,
                TrackCode = data.NewData.TrackCode
            };

            // Extract Normalized Baseline
            PopulateNormalizedData(packet.Normalized, data.NewData);

            // Extract Game-Specific Raw Buffer if enabled
            if (CaptureRawPayload)
            {
                try
                {
                    packet.RawPayload = data.NewData.GetRawDataObject();
                }
                catch (Exception ex)
                {
                    packet.RawPayload = new { Error = ex.Message };
                }
            }

            // Extract Dynamic SimHub Properties if enabled
            if (CaptureAllProperties)
            {
                packet.Properties = ExtractDynamicProperties(pluginManager);
            }

            // Non-blocking enqueue
            if (_packetQueue.TryAdd(packet))
            {
                FramesCaptured++;
            }
        }

        private void PopulateNormalizedData(NormalizedData target, StatusData src)
        {
            target.SpeedKmh = src.SpeedKmh;
            target.Rpms = src.Rpms;
            target.MaxRpm = src.MaxRpm;
            target.Gear = src.Gear;
            target.Throttle = src.Throttle;
            target.Brake = src.Brake;
            target.Clutch = src.Clutch;
            target.SteeringAngle = src.SteeringAngle;

            target.Pitch = src.OrientationPitch;
            target.Roll = src.OrientationRoll;
            target.Yaw = src.OrientationYaw;

            target.AccelerationSurge = src.AccelerationSurge;
            target.AccelerationSway = src.AccelerationSway;
            target.AccelerationHeave = src.AccelerationHeave;

            target.CompletedLaps = src.CompletedLaps;
            target.CurrentLapTime = src.CurrentLapTime;
            target.BestLapTime = src.BestLapTime;
            target.LastLapTime = src.LastLapTime;
            target.TrackPositionPercent = src.TrackPositionPercent;

            target.Fuel = src.Fuel;
            target.MaxFuel = src.MaxFuel;

            // Tyres: FL=0, FR=1, RL=2, RR=3
            target.Tyres.TemperaturesC = new[]
            {
                src.TyreTemperatureFrontLeft,
                src.TyreTemperatureFrontRight,
                src.TyreTemperatureRearLeft,
                src.TyreTemperatureRearRight
            };

            target.Tyres.PressuresKpa = new[]
            {
                src.TyrePressureFrontLeft,
                src.TyrePressureFrontRight,
                src.TyrePressureRearLeft,
                src.TyrePressureRearRight
            };

            target.Tyres.WearPercent = new[]
            {
                src.TyreWearFrontLeft,
                src.TyreWearFrontRight,
                src.TyreWearRearLeft,
                src.TyreWearRearRight
            };
        }

        private Dictionary<string, object?> ExtractDynamicProperties(PluginManager pluginManager)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Reflect on PluginManager.GetAllProperties() or internal PropertyList
                var method = pluginManager.GetType().GetMethod("GetAllProperties", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    var props = method.Invoke(pluginManager, null) as IEnumerable<string>;
                    if (props != null)
                    {
                        foreach (var prop in props)
                        {
                            dict[prop] = pluginManager.GetPropertyValue(prop);
                        }
                    }
                }
                else
                {
                    // Fallback to internal properties field if GetAllProperties is private or renamed
                    var field = pluginManager.GetType().GetField("properties", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field?.GetValue(pluginManager) is IDictionary<string, object> rawMap)
                    {
                        foreach (var kvp in rawMap)
                        {
                            dict[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Graceful fallback to avoid interrupting telemetry loop
            }

            DiscoveredPropertyCount = dict.Count;
            return dict;
        }

        private void ExportSchemaCatalog(PluginManager pluginManager, string sessionJsonlPath)
        {
            try
            {
                var catalogPath = Path.ChangeExtension(sessionJsonlPath, ".catalog.json");
                var properties = ExtractDynamicProperties(pluginManager);

                var descriptors = properties.Select(kvp => new PropertyDescriptor
                {
                    Name = kvp.Key,
                    TypeName = kvp.Value?.GetType().FullName ?? "null",
                    SampleValue = kvp.Value
                }).OrderBy(p => p.Name).ToList();

                var json = JsonConvert.SerializeObject(descriptors, Formatting.Indented);
                File.WriteAllText(catalogPath, json);
            }
            catch (Exception) { /* schema dump best-effort */ }
        }

        private void ProcessWriteQueue(CancellationToken token)
        {
            var batchCount = 0;
            while (!token.IsCancellationRequested || _packetQueue.Count > 0)
            {
                try
                {
                    if (_packetQueue.TryTake(out var packet, 100, token))
                    {
                        var line = JsonConvert.SerializeObject(packet, Formatting.None);
                        _currentWriter?.WriteLine(line);
                        batchCount++;

                        if (batchCount >= 50)
                        {
                            _currentWriter?.Flush();
                            batchCount = 0;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Prevent thread crash
                }
            }

            _currentWriter?.Flush();
        }

        private static string SanitizeFilename(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalids.Contains(c) ? '_' : c));
        }

        public void Dispose()
        {
            if (IsCapturing)
            {
                _cts?.Cancel();
                _currentWriter?.Dispose();
            }
            _packetQueue.Dispose();
            _cts?.Dispose();
        }
    }
}
