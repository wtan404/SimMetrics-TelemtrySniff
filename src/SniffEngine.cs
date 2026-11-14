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
using SimMetryx.TelemetrySniff.Models;

namespace SimMetryx.TelemetrySniff
{
    public enum SnifferState
    {
        Disabled,
        Standby,
        Armed,
        Capturing
    }

    public class SniffSettings
    {
        public bool IsEnabled { get; set; } = true;
        public string OutputDirectory { get; set; } = @"D:\Projects\SimMetrics-TelemtrySniff\logs";
        public int TargetHz { get; set; } = 30;
        public bool CaptureRawPayload { get; set; } = true;
        public bool CaptureAllProperties { get; set; } = true;
    }

    /// <summary>
    /// High-throughput telemetry capture engine designed strictly for raw packet collection,
    /// dynamic property discovery, and hardware buffer sniffing for RaceBot reverse engineering.
    /// </summary>
    public class SniffEngine : IDisposable
    {
        private readonly BlockingCollection<TelemetryPacket> _packetQueue = new BlockingCollection<TelemetryPacket>(5000);
        private CancellationTokenSource? _cts;
        private Task? _writerTask;
        private StreamWriter? _currentWriter;
        private DateTime _lastSampleTime = DateTime.MinValue;

        // Runtime State
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                if (!_isEnabled)
                {
                    IsArmed = false;
                    if (IsCapturing)
                    {
                        StopCapture(null);
                    }
                }
                SaveSettings();
            }
        }

        public bool IsArmed { get; private set; }
        public bool IsCapturing { get; private set; }

        public SnifferState State
        {
            get
            {
                if (!IsEnabled) return SnifferState.Disabled;
                if (IsCapturing) return SnifferState.Capturing;
                if (IsArmed) return SnifferState.Armed;
                return SnifferState.Standby;
            }
        }

        // Configuration Settings
        private int _targetHz = 30;
        public int TargetHz
        {
            get => _targetHz;
            set { _targetHz = value; SaveSettings(); }
        }

        private bool _captureRawPayload = true;
        public bool CaptureRawPayload
        {
            get => _captureRawPayload;
            set { _captureRawPayload = value; SaveSettings(); }
        }

        private bool _captureAllProperties = true;
        public bool CaptureAllProperties
        {
            get => _captureAllProperties;
            set { _captureAllProperties = value; SaveSettings(); }
        }

        private string _outputDirectory = @"D:\Projects\SimMetrics-TelemtrySniff\logs";
        public string OutputDirectory
        {
            get => _outputDirectory;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                _outputDirectory = value;
                SaveSettings();
            }
        }

        // Capture Statistics & File Pointers
        public long FramesCaptured { get; private set; }
        public int DiscoveredPropertyCount { get; private set; }
        public string? CurrentSessionFile { get; private set; }
        public string? LastCatalogFile { get; private set; }
        public string? LastManifestFile { get; private set; }
        public CaptureManifest? CurrentManifest { get; private set; }

        public SniffEngine()
        {
            LoadSettings();
        }

        public void ArmCapture()
        {
            if (!IsEnabled || IsCapturing) return;
            IsArmed = true;
        }

        public void DisarmCapture()
        {
            IsArmed = false;
        }

        public void ToggleArmOrCapture(bool isGameRunning, string? gameName, PluginManager? pluginManager)
        {
            if (!IsEnabled) return;

            if (IsCapturing)
            {
                StopCapture(pluginManager);
                IsArmed = false;
            }
            else if (IsArmed)
            {
                IsArmed = false;
            }
            else
            {
                if (isGameRunning)
                {
                    var safeGame = string.IsNullOrWhiteSpace(gameName) || gameName == "None" ? "Simulator" : gameName!;
                    StartCapture(safeGame);
                }
                else
                {
                    ArmCapture();
                }
            }
        }

        public void StartCapture(string gameName, string? sessionType = null, string? car = null, string? track = null)
        {
            if (!IsEnabled || IsCapturing) return;

            try
            {
                if (!Directory.Exists(OutputDirectory))
                {
                    Directory.CreateDirectory(OutputDirectory);
                }
            }
            catch (Exception)
            {
                // Fallback to Documents if configured path is inaccessible
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                OutputDirectory = Path.Combine(docs, "SimMetryx", "TelemetrySniff");
                Directory.CreateDirectory(OutputDirectory);
            }

            var safeGame = string.IsNullOrWhiteSpace(gameName) || gameName == "None" ? "Simulator" : SanitizeFilename(gameName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filename = $"sniff_{safeGame}_{timestamp}.jsonl";
            CurrentSessionFile = Path.Combine(OutputDirectory, filename);

            _currentWriter = new StreamWriter(new FileStream(CurrentSessionFile, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = false
            };

            _cts = new CancellationTokenSource();
            _writerTask = Task.Run(() => ProcessWriteQueue(_cts.Token));
            FramesCaptured = 0;
            IsCapturing = true;
            IsArmed = false;

            CurrentManifest = new CaptureManifest
            {
                GameName = safeGame,
                SessionType = sessionType ?? "Unknown",
                Vehicle = car ?? "Unknown",
                Track = track ?? "Unknown",
                StartTimeUtc = DateTime.UtcNow,
                TargetHz = TargetHz,
                RawBufferCaptured = CaptureRawPayload,
                DynamicPropertiesCaptured = CaptureAllProperties,
                JsonlFilePath = CurrentSessionFile
            };
        }

        public void StopCapture(PluginManager? pluginManager)
        {
            if (!IsCapturing) return;

            IsCapturing = false;
            _cts?.Cancel();

            try
            {
                _writerTask?.Wait(2000);
            }
            catch (Exception) { /* ignored */ }

            try
            {
                _currentWriter?.Flush();
                _currentWriter?.Dispose();
            }
            catch (Exception) { /* ignored */ }
            _currentWriter = null;

            if (CurrentSessionFile != null)
            {
                if (pluginManager != null && CaptureAllProperties)
                {
                    ExportSchemaCatalog(pluginManager, CurrentSessionFile);
                }

                ExportManifest(CurrentSessionFile);
            }
        }

        /// <summary>
        /// Invoked every frame from SimHub's DataUpdate lifecycle.
        /// </summary>
        public void ProcessFrame(PluginManager pluginManager, ref GameData data)
        {
            if (!IsEnabled) return;

            var isRunning = data?.GameRunning ?? false;

            // Handle Simulator Startup while Armed
            if (isRunning && IsArmed && !IsCapturing)
            {
                var activeGame = pluginManager.GameName;
                if (string.IsNullOrWhiteSpace(activeGame) || activeGame == "None")
                {
                    activeGame = "Simulator";
                }

                var sessionType = data?.NewData?.SessionTypeName;
                var car = data?.NewData?.CarModel;
                var track = data?.NewData?.TrackCode;

                StartCapture(activeGame, sessionType, car, track);
            }

            // Handle Simulator Exit while Recording
            if (!isRunning && IsCapturing)
            {
                StopCapture(pluginManager);
                return;
            }

            // Throttled frame capture
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
                SessionType = data.NewData?.SessionTypeName ?? "Unknown",
                CarModel = data.NewData?.CarModel,
                TrackCode = data.NewData?.TrackCode
            };

            // Extract Normalized Baseline
            if (data.NewData != null)
            {
                PopulateNormalizedData(packet.Normalized, data.NewData);
            }

            // Extract Game-Specific Raw Buffer if enabled
            if (CaptureRawPayload && data.NewData != null)
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

            // Keep manifest session info up to date
            if (CurrentManifest != null && data.NewData != null)
            {
                if (CurrentManifest.SessionType == "Unknown" && !string.IsNullOrEmpty(data.NewData.SessionTypeName))
                    CurrentManifest.SessionType = data.NewData.SessionTypeName;

                if (CurrentManifest.Vehicle == "Unknown" && !string.IsNullOrEmpty(data.NewData.CarModel))
                    CurrentManifest.Vehicle = data.NewData.CarModel;

                if (CurrentManifest.Track == "Unknown" && !string.IsNullOrEmpty(data.NewData.TrackCode))
                    CurrentManifest.Track = data.NewData.TrackCode;
            }
        }

        private void PopulateNormalizedData(NormalizedData target, StatusDataBase src)
        {
            target.SpeedKmh = src.SpeedKmh;
            target.Rpms = src.Rpms;
            target.MaxRpm = src.MaxRpm;
            target.Gear = src.Gear;
            target.Throttle = src.Throttle;
            target.Brake = src.Brake;
            target.Clutch = src.Clutch;
            target.SteeringAngle = 0.0;

            target.Pitch = src.OrientationPitch;
            target.Roll = src.OrientationRoll;
            target.Yaw = src.OrientationYaw;

            target.AccelerationSurge = src.AccelerationSurge ?? 0.0;
            target.AccelerationSway = src.AccelerationSway ?? 0.0;
            target.AccelerationHeave = src.AccelerationHeave ?? 0.0;

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
                var method = pluginManager.GetType().GetMethod("GetAllProperties", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    if (method.Invoke(pluginManager, null) is IEnumerable<string> props)
                    {
                        foreach (var prop in props)
                        {
                            dict[prop] = pluginManager.GetPropertyValue(prop);
                        }
                    }
                }
                else
                {
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
                // Graceful fallback
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
                LastCatalogFile = catalogPath;
            }
            catch (Exception) { /* schema dump best-effort */ }
        }

        private void ExportManifest(string sessionJsonlPath)
        {
            if (CurrentManifest == null) return;

            try
            {
                CurrentManifest.EndTimeUtc = DateTime.UtcNow;
                CurrentManifest.TotalFramesCaptured = FramesCaptured;
                CurrentManifest.DiscoveredPropertiesCount = DiscoveredPropertyCount;
                CurrentManifest.JsonlFilePath = sessionJsonlPath;
                CurrentManifest.CatalogFilePath = Path.ChangeExtension(sessionJsonlPath, ".catalog.json");

                LastManifestFile = Path.ChangeExtension(sessionJsonlPath, ".manifest.json");
                File.WriteAllText(LastManifestFile, CurrentManifest.ToJson(indented: true));
            }
            catch (Exception) { /* best effort */ }
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

            try { _currentWriter?.Flush(); } catch { }
        }

        private static string SanitizeFilename(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalids.Contains(c) ? '_' : c));
        }

        private static string GetSettingsFilePath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimMetryx");
            return Path.Combine(folder, "SimMetryxObserver.settings.json");
        }

        private void LoadSettings()
        {
            // Default directory logic
            var defaultLogs = @"D:\Projects\SimMetrics-TelemtrySniff\logs";
            if (Directory.Exists(@"D:\Projects") || Directory.Exists(Path.GetPathRoot(defaultLogs) ?? string.Empty))
            {
                _outputDirectory = defaultLogs;
            }
            else
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                _outputDirectory = Path.Combine(docs, "SimMetryx", "TelemetrySniff");
            }

            try
            {
                var file = GetSettingsFilePath();
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var settings = JsonConvert.DeserializeObject<SniffSettings>(json);
                    if (settings != null)
                    {
                        _isEnabled = settings.IsEnabled;
                        if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
                        {
                            _outputDirectory = settings.OutputDirectory;
                        }
                        _targetHz = settings.TargetHz;
                        _captureRawPayload = settings.CaptureRawPayload;
                        _captureAllProperties = settings.CaptureAllProperties;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to defaults
            }
        }

        public void SaveSettings()
        {
            try
            {
                var file = GetSettingsFilePath();
                var dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var settings = new SniffSettings
                {
                    IsEnabled = _isEnabled,
                    OutputDirectory = _outputDirectory,
                    TargetHz = _targetHz,
                    CaptureRawPayload = _captureRawPayload,
                    CaptureAllProperties = _captureAllProperties
                };

                File.WriteAllText(file, JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            catch (Exception)
            {
                // Settings save best effort
            }
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
