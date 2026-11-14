using System;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;
using SimMetryx.TelemetrySniff.UI;

namespace SimMetryx.TelemetrySniff
{
    [PluginDescription("Universal telemetry sniffer, raw buffer dumper, and channel discovery tool for RaceBot.")]
    [PluginAuthor("SimMetryx")]
    [PluginName("SimMetryx Observer")]
    public class TelemetrySniffPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public PluginManager? PluginManager { get; set; }
        public SniffEngine Engine { get; } = new SniffEngine();
        private SniffPluginUI? _settingsControl;

        private string _lastReportedGame = string.Empty;
        private bool _lastReportedRunning = false;

        /// <summary>
        /// Plugin initialization upon SimHub startup.
        /// </summary>
        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;

            // Declare exposed properties in SimHub
            pluginManager.AddProperty("IsEnabled", GetType(), Engine.IsEnabled);
            pluginManager.AddProperty("IsArmed", GetType(), Engine.IsArmed);
            pluginManager.AddProperty("IsCapturing", GetType(), Engine.IsCapturing);
            pluginManager.AddProperty("ActiveGame", GetType(), "None");
            pluginManager.AddProperty("IsGameRunning", GetType(), false);
            pluginManager.AddProperty("SessionType", GetType(), "None");
            pluginManager.AddProperty("CurrentVehicle", GetType(), "None");
            pluginManager.AddProperty("CurrentTrack", GetType(), "None");
            pluginManager.AddProperty("StatusIndicator", GetType(), "⚪ STANDBY: No Simulator Running");
            pluginManager.AddProperty("FramesCaptured", GetType(), 0L);
            pluginManager.AddProperty("DiscoveredPropertiesCount", GetType(), 0);
            pluginManager.AddProperty("CurrentSessionFile", GetType(), string.Empty);
            pluginManager.AddProperty("LastManifestFile", GetType(), string.Empty);
            pluginManager.AddProperty("LastCatalogFile", GetType(), string.Empty);
            pluginManager.AddProperty("TargetHz", GetType(), Engine.TargetHz);

            // Register wheel / keyboard bindable actions
            pluginManager.AddAction("ToggleMasterPower", GetType(), (a, b) => Engine.IsEnabled = !Engine.IsEnabled);
            pluginManager.AddAction("ToggleCapture", GetType(), (a, b) => ToggleCapture());
            pluginManager.AddAction("StartCapture", GetType(), (a, b) => StartCapture());
            pluginManager.AddAction("StopCapture", GetType(), (a, b) => StopCapture());
        }

        /// <summary>
        /// Called every tick when SimHub reads incoming game telemetry.
        /// </summary>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            var isRunning = data.GameRunning;
            var activeGame = isRunning ? (pluginManager.GameName ?? "ActiveSim") : "None";

            if (!Engine.IsEnabled)
            {
                pluginManager.SetPropertyValue("IsEnabled", GetType(), false);
                pluginManager.SetPropertyValue("IsArmed", GetType(), false);
                pluginManager.SetPropertyValue("IsCapturing", GetType(), false);
                pluginManager.SetPropertyValue("ActiveGame", GetType(), activeGame);
                pluginManager.SetPropertyValue("IsGameRunning", GetType(), isRunning);
                pluginManager.SetPropertyValue("StatusIndicator", GetType(), "⏸️ DISABLED: Observer Off (Control Panel)");
                return;
            }

            Engine.ProcessFrame(pluginManager, ref data);

            var sessionType = data.NewData != null && !string.IsNullOrEmpty(data.NewData.SessionTypeName) ? data.NewData.SessionTypeName : "Unknown";
            var car = data.NewData != null && !string.IsNullOrEmpty(data.NewData.CarModel) ? data.NewData.CarModel : "Unknown Vehicle";
            var track = data.NewData != null && !string.IsNullOrEmpty(data.NewData.TrackCode) ? data.NewData.TrackCode : "Unknown Track";

            // Track game launch transitions
            if (isRunning != _lastReportedRunning || (!string.IsNullOrEmpty(activeGame) && activeGame != _lastReportedGame))
            {
                _lastReportedGame = activeGame;
                _lastReportedRunning = isRunning;
            }

            string statusIndicator;
            if (Engine.IsCapturing)
            {
                statusIndicator = $"🔴 RECORDING: {activeGame} [{sessionType}] @ {track}";
            }
            else if (Engine.IsArmed)
            {
                statusIndicator = "🟡 ARMED: Waiting for Simulator launch...";
            }
            else if (isRunning)
            {
                statusIndicator = $"🟢 LIVE: {activeGame} [{sessionType}] @ {track}";
            }
            else
            {
                statusIndicator = "⚪ STANDBY: No Simulator Running";
            }

            // Update registered SimHub properties
            pluginManager.SetPropertyValue("IsEnabled", GetType(), true);
            pluginManager.SetPropertyValue("IsArmed", GetType(), Engine.IsArmed);
            pluginManager.SetPropertyValue("IsCapturing", GetType(), Engine.IsCapturing);
            pluginManager.SetPropertyValue("ActiveGame", GetType(), activeGame);
            pluginManager.SetPropertyValue("IsGameRunning", GetType(), isRunning);
            pluginManager.SetPropertyValue("SessionType", GetType(), isRunning ? sessionType : "None");
            pluginManager.SetPropertyValue("CurrentVehicle", GetType(), isRunning ? car : "None");
            pluginManager.SetPropertyValue("CurrentTrack", GetType(), isRunning ? track : "None");
            pluginManager.SetPropertyValue("StatusIndicator", GetType(), statusIndicator);
            pluginManager.SetPropertyValue("FramesCaptured", GetType(), Engine.FramesCaptured);
            pluginManager.SetPropertyValue("DiscoveredPropertiesCount", GetType(), Engine.DiscoveredPropertyCount);
            pluginManager.SetPropertyValue("CurrentSessionFile", GetType(), Engine.CurrentSessionFile ?? string.Empty);
            pluginManager.SetPropertyValue("LastManifestFile", GetType(), Engine.LastManifestFile ?? string.Empty);
            pluginManager.SetPropertyValue("LastCatalogFile", GetType(), Engine.LastCatalogFile ?? string.Empty);
            pluginManager.SetPropertyValue("TargetHz", GetType(), Engine.TargetHz);
        }

        /// <summary>
        /// Called when SimHub closes or plugin is unloaded.
        /// </summary>
        public void End(PluginManager pluginManager)
        {
            if (Engine.IsCapturing)
            {
                Engine.StopCapture(pluginManager);
            }
            Engine.Dispose();
        }

        public void StartCapture()
        {
            if (PluginManager == null || !Engine.IsEnabled || Engine.IsCapturing) return;

            var isRunning = PluginManager.GetPropertyValue("IsGameRunning") is bool running && running;
            if (isRunning)
            {
                var game = PluginManager.GameName;
                if (string.IsNullOrWhiteSpace(game) || game == "None") game = "Simulator";
                Engine.StartCapture(game);
            }
            else
            {
                Engine.ArmCapture();
            }
        }

        public void StopCapture()
        {
            if (PluginManager == null) return;
            if (Engine.IsCapturing)
            {
                Engine.StopCapture(PluginManager);
            }
            Engine.DisarmCapture();
        }

        public void ToggleCapture()
        {
            if (PluginManager == null || !Engine.IsEnabled) return;
            var isRunning = PluginManager.GetPropertyValue("IsGameRunning") is bool running && running;
            Engine.ToggleArmOrCapture(isRunning, PluginManager.GameName, PluginManager);
        }

        /// <summary>
        /// Returns the WPF control displayed inside SimHub's Plugins tab.
        /// </summary>
        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            try
            {
                return _settingsControl ??= new SniffPluginUI(this);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to initialize SimMetryx Observer UI: {ex}");
                return new ContentControl
                {
                    Content = new TextBlock { Text = $"SimMetryx Observer UI Error: {ex.Message}", Margin = new System.Windows.Thickness(20) }
                };
            }
        }

        public ImageSource? PictureIcon => null;
        public string LeftMenuTitle => "SimMetryx Observer";
    }
}
