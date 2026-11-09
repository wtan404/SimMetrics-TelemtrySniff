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
using SessionSummary = SimMetrics.TelemetrySniff.Core.Models.SessionSummary;

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
        public string? LastSummaryMarkdownFile { get; private set; }
        public string? LastSummaryJsonFile { get; private set; }
        public SessionSummary? CurrentSessionSummary { get; private set; }

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

            CurrentSessionSummary = new SessionSummary
            {
                GameName = safeGame,
                StartTimeUtc = DateTime.UtcNow,
                TargetHz = TargetHz,
                RawDataCaptured = CaptureRawPayload,
                MinTyreTempC = 999.0,
                MaxTyreTempC = 0.0
            };
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

            // Generate schema catalog and AI-readable session summary alongside the recording
            if (CurrentSessionFile != null)
            {
                ExportSchemaCatalog(pluginManager, CurrentSessionFile);
                ExportSessionSummary(CurrentSessionFile);
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

            // Update session performance envelope for AI summary
            if (CurrentSessionSummary != null)
            {
                if (CurrentSessionSummary.SessionType == "Unknown" && !string.IsNullOrEmpty(data.SessionTypeName))
                    CurrentSessionSummary.SessionType = data.SessionTypeName;

                if (CurrentSessionSummary.CarModel == "Unknown" && !string.IsNullOrEmpty(data.NewData.CarModel))
                    CurrentSessionSummary.CarModel = data.NewData.CarModel;

                if (CurrentSessionSummary.TrackCode == "Unknown" && !string.IsNullOrEmpty(data.NewData.TrackCode))
                    CurrentSessionSummary.TrackCode = data.NewData.TrackCode;

                if (data.NewData.SpeedKmh > CurrentSessionSummary.TopSpeedKmh)
                    CurrentSessionSummary.TopSpeedKmh = data.NewData.SpeedKmh;

                if (data.NewData.Rpms > CurrentSessionSummary.MaxRpm)
                    CurrentSessionSummary.MaxRpm = data.NewData.Rpms;

                if (data.NewData.CompletedLaps > CurrentSessionSummary.CompletedLaps)
                    CurrentSessionSummary.CompletedLaps = data.NewData.CompletedLaps;

                if (data.NewData.BestLapTime.HasValue)
                    CurrentSessionSummary.BestLapTime = data.NewData.BestLapTime;

                if (data.NewData.LastLapTime.HasValue)
                    CurrentSessionSummary.LastLapTime = data.NewData.LastLapTime;

                if (CurrentSessionSummary.FuelStart <= 0 && data.NewData.Fuel > 0)
                    CurrentSessionSummary.FuelStart = data.NewData.Fuel;
                CurrentSessionSummary.FuelEnd = data.NewData.Fuel;

                var tyres = new[]
                {
                    data.NewData.TyreTemperatureFrontLeft,
                    data.NewData.TyreTemperatureFrontRight,
                    data.NewData.TyreTemperatureRearLeft,
                    data.NewData.TyreTemperatureRearRight
                };
                foreach (var t in tyres)
                {
                    if (t > 0)
                    {
                        if (t < CurrentSessionSummary.MinTyreTempC) CurrentSessionSummary.MinTyreTempC = t;
                        if (t > CurrentSessionSummary.MaxTyreTempC) CurrentSessionSummary.MaxTyreTempC = t;
                    }
                }
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

        private void ExportSessionSummary(string sessionJsonlPath)
        {
            if (CurrentSessionSummary == null) return;

            try
            {
                CurrentSessionSummary.EndTimeUtc = DateTime.UtcNow;
                CurrentSessionSummary.TotalFrames = FramesCaptured;
                CurrentSessionSummary.DiscoveredChannelsCount = DiscoveredPropertyCount;
                CurrentSessionSummary.JsonlFilePath = sessionJsonlPath;
                CurrentSessionSummary.CatalogFilePath = Path.ChangeExtension(sessionJsonlPath, ".catalog.json");
                if (CurrentSessionSummary.MinTyreTempC >= 999.0)
                {
                    CurrentSessionSummary.MinTyreTempC = 0.0;
                }

                LastSummaryMarkdownFile = Path.ChangeExtension(sessionJsonlPath, ".summary.md");
                LastSummaryJsonFile = Path.ChangeExtension(sessionJsonlPath, ".summary.json");

                File.WriteAllText(LastSummaryMarkdownFile, CurrentSessionSummary.ToMarkdown());
                File.WriteAllText(LastSummaryJsonFile, CurrentSessionSummary.ToJson(indented: true));
            }
            catch (Exception)
            {
                // Best-effort export to prevent capture loop exceptions
            }
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
