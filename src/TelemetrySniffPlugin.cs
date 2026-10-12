using System;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;
using SimMetrics.TelemetrySniff.UI;

namespace SimMetrics.TelemetrySniff
{
    [PluginDescription("Captures and catalogs telemetry datasets across AMS2, iRacing, AC, BeamNG, and other sims for reverse engineering and dataset mapping.")]
    [PluginAuthor("SimMetrics")]
    [PluginName("SimMetrics Telemetry Sniff")]
    public class TelemetrySniffPlugin : IPlugin, IDataPlugin, IWPFAddIn
    {
        public PluginManager? PluginManager { get; private set; }
        public SniffEngine Engine { get; } = new SniffEngine();
        private SniffPluginUI? _settingsControl;

        /// <summary>
        /// Plugin initialization upon SimHub startup.
        /// </summary>
        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;

            // Declare exposed properties in SimHub
            pluginManager.AddProperty("IsCapturing", GetType(), false);
            pluginManager.AddProperty("FramesCaptured", GetType(), 0L);
            pluginManager.AddProperty("DiscoveredPropertiesCount", GetType(), 0);
            pluginManager.AddProperty("CurrentSessionFile", GetType(), string.Empty);
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

            // Update registered SimHub properties
            pluginManager.SetPropertyValue("IsCapturing", GetType(), Engine.IsCapturing);
            pluginManager.SetPropertyValue("FramesCaptured", GetType(), Engine.FramesCaptured);
            pluginManager.SetPropertyValue("DiscoveredPropertiesCount", GetType(), Engine.DiscoveredPropertyCount);
            pluginManager.SetPropertyValue("CurrentSessionFile", GetType(), Engine.CurrentSessionFile ?? string.Empty);
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
            Engine.StartCapture(PluginManager.GameName);
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
        public string LeftMenuTitle => "Telemetry Sniff";
    }
}
