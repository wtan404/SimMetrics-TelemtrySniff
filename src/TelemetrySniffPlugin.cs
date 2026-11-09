using System;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;
using SimMetrics.TelemetrySniff.UI;

namespace SimMetrics.TelemetrySniff
{
    [PluginDescription("Universal telemetry observer and state data dumper across all racing simulators for testing and reverse engineering.")]
    [PluginAuthor("SimMetryx")]
    [PluginName("SimMetryx Observer")]
    public class TelemetrySniffPlugin : IPlugin, IDataPlugin, IWPFAddIn
    {
        public PluginManager? PluginManager { get; private set; }
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
            pluginManager.AddProperty("IsCapturing", GetType(), false);
            pluginManager.AddProperty("ActiveGame", GetType(), "None");
            pluginManager.AddProperty("IsGameRunning", GetType(), false);
            pluginManager.AddProperty("SessionType", GetType(), "None");
            pluginManager.AddProperty("CurrentVehicle", GetType(), "None");
            pluginManager.AddProperty("CurrentTrack", GetType(), "None");
            pluginManager.AddProperty("StatusIndicator", GetType(), "⚪ STANDBY: No Simulator Running");
            pluginManager.AddProperty("FramesCaptured", GetType(), 0L);
            pluginManager.AddProperty("DiscoveredPropertiesCount", GetType(), 0);
            pluginManager.AddProperty("CurrentSessionFile", GetType(), string.Empty);
            pluginManager.AddProperty("LastSummaryFile", GetType(), string.Empty);
            pluginManager.AddProperty("TargetHz", GetType(), Engine.TargetHz);

            // Register wheel / keyboard bindable actions
            pluginManager.AddAction("ToggleCapture", GetType(), (a, b) => ToggleCapture());
            pluginManager.AddAction("StartCapture", GetType(), (a, b) => StartCapture());
            pluginManager.AddAction("StopCapture", GetType(), (a, b) => StopCapture());
        }

        /// <summary>
        /// Called every tick when SimHub reads incoming game telemetry.
        /// </summary>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            Engine.ProcessFrame(pluginManager, ref data);

            var activeGame = pluginManager.GameName ?? (data.GameRunning ? "UnknownSim" : "None");
            var isRunning = data.GameRunning;
            var sessionType = !string.IsNullOrEmpty(data.SessionTypeName) ? data.SessionTypeName : "Unknown";
            var car = data.NewData != null && !string.IsNullOrEmpty(data.NewData.CarModel) ? data.NewData.CarModel : "Unknown Vehicle";
            var track = data.NewData != null && !string.IsNullOrEmpty(data.NewData.TrackCode) ? data.NewData.TrackCode : "Unknown Track";

            // Track game launch transitions
            if (isRunning != _lastReportedRunning || (!string.IsNullOrEmpty(activeGame) && activeGame != _lastReportedGame))
            {
                _lastReportedGame = activeGame;
                _lastReportedRunning = isRunning;
            }

            var statusIndicator = isRunning
                ? $"{(Engine.IsCapturing ? "🔴 RECORDING" : "🟢 LIVE")}: {activeGame} [{sessionType}] @ {track}"
                : "⚪ STANDBY: No Simulator Running";

            // Update registered SimHub properties
            pluginManager.SetPropertyValue("IsCapturing", GetType(), Engine.IsCapturing);
            pluginManager.SetPropertyValue("ActiveGame", GetType(), isRunning ? activeGame : "None");
            pluginManager.SetPropertyValue("IsGameRunning", GetType(), isRunning);
            pluginManager.SetPropertyValue("SessionType", GetType(), isRunning ? sessionType : "None");
            pluginManager.SetPropertyValue("CurrentVehicle", GetType(), isRunning ? car : "None");
            pluginManager.SetPropertyValue("CurrentTrack", GetType(), isRunning ? track : "None");
            pluginManager.SetPropertyValue("StatusIndicator", GetType(), statusIndicator);
            pluginManager.SetPropertyValue("FramesCaptured", GetType(), Engine.FramesCaptured);
            pluginManager.SetPropertyValue("DiscoveredPropertiesCount", GetType(), Engine.DiscoveredPropertyCount);
            pluginManager.SetPropertyValue("CurrentSessionFile", GetType(), Engine.CurrentSessionFile ?? string.Empty);
            pluginManager.SetPropertyValue("LastSummaryFile", GetType(), Engine.LastSummaryMarkdownFile ?? string.Empty);
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
            if (PluginManager == null || Engine.IsCapturing) return;
            var game = PluginManager.GameName;
            if (string.IsNullOrWhiteSpace(game))
            {
                game = "Simulator";
            }
            Engine.StartCapture(game);
        }

        public void StopCapture()
        {
            if (PluginManager == null || !Engine.IsCapturing) return;
            Engine.StopCapture(PluginManager);
        }

        public void ToggleCapture()
        {
            if (Engine.IsCapturing)
            {
                StopCapture();
            }
            else
            {
                StartCapture();
            }
        }

        /// <summary>
        /// Returns the WPF control displayed inside SimHub's Plugins tab.
        /// </summary>
        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return _settingsControl ??= new SniffPluginUI(this);
        }

        public ImageSource? PictureIcon => null;
        public string LeftMenuTitle => "SimMetryx Observer";
    }
}
