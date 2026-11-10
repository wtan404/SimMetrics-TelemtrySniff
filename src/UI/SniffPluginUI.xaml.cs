using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SimMetryx.TelemetrySniff.UI
{
    public partial class SniffPluginUI : UserControl
    {
        private readonly TelemetrySniffPlugin _plugin;
        private readonly DispatcherTimer _uiTimer;

        public SniffPluginUI(TelemetrySniffPlugin plugin)
        {
            _plugin = plugin;
            InitializeComponent();

            TxtOutputDir.Text = _plugin.Engine.OutputDirectory;
            SliderHz.Value = _plugin.Engine.TargetHz;

            // Timer for UI status refresh at 4 Hz
            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            var pm = _plugin.PluginManager;
            var isRunning = pm?.GetPropertyValue("IsGameRunning") is bool running && running;
            var activeGame = pm?.GetPropertyValue("ActiveGame") as string ?? (isRunning ? (pm?.GameName ?? "Active Sim") : "None");
            var sessionType = pm?.GetPropertyValue("SessionType") as string ?? "Unknown";
            var car = pm?.GetPropertyValue("CurrentVehicle") as string ?? "Unknown Car";
            var track = pm?.GetPropertyValue("CurrentTrack") as string ?? "Unknown Track";

            // Update Simulator Title & Detail
            if (isRunning && activeGame != "None")
            {
                TxtGameName.Text = activeGame;
                TxtSessionDetail.Text = $"{sessionType} • {track} • {car}";
            }
            else
            {
                TxtGameName.Text = "No Game Running (Standby)";
                TxtSessionDetail.Text = "Waiting for simulator session launch...";
            }

            // Update Badge & Toggle Button State
            if (_plugin.Engine.IsCapturing)
            {
                BadgeIndicator.Background = new SolidColorBrush(Color.FromRgb(220, 50, 50));
                TxtBadgeStatus.Text = "OBSERVING";

                BtnToggleCapture.Content = "⏹️ STOP OBSERVER & EXPORT";
                BtnToggleCapture.Background = new SolidColorBrush(Color.FromRgb(180, 40, 40));
            }
            else if (isRunning)
            {
                BadgeIndicator.Background = new SolidColorBrush(Color.FromRgb(46, 139, 87));
                TxtBadgeStatus.Text = "SIM RUNNING";

                BtnToggleCapture.Content = "▶️ START OBSERVER (DUMP)";
                BtnToggleCapture.Background = new SolidColorBrush(Color.FromRgb(14, 112, 166));
            }
            else
            {
                BadgeIndicator.Background = new SolidColorBrush(Color.FromRgb(90, 90, 90));
                TxtBadgeStatus.Text = "STANDBY";

                BtnToggleCapture.Content = "▶️ START OBSERVER (DUMP)";
                BtnToggleCapture.Background = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            }

            TxtFrames.Text = _plugin.Engine.FramesCaptured.ToString("N0");
            TxtPropertiesCount.Text = _plugin.Engine.DiscoveredPropertyCount.ToString("N0");

            // Update AI Summary Link
            var lastSummary = _plugin.Engine.LastSummaryMarkdownFile;
            if (!string.IsNullOrEmpty(lastSummary) && File.Exists(lastSummary))
            {
                TxtLastSummary.Text = lastSummary;
                BtnOpenSummary.IsEnabled = true;
            }
            else
            {
                TxtLastSummary.Text = "No observation session completed yet";
                BtnOpenSummary.IsEnabled = false;
            }
        }

        private void BtnToggleCapture_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ToggleCapture();
        }

        private void SliderHz_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtHz == null) return;
            var hz = (int)e.NewValue;
            TxtHz.Text = $"{hz} Hz";
            if (_plugin?.Engine != null)
            {
                _plugin.Engine.TargetHz = hz;
            }
        }

        private void ChkConfig_Changed(object sender, RoutedEventArgs e)
        {
            if (_plugin?.Engine == null) return;
            _plugin.Engine.CaptureRawPayload = ChkRawPayload.IsChecked == true;
            _plugin.Engine.CaptureAllProperties = ChkDynamicProps.IsChecked == true;
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = _plugin.Engine.OutputDirectory;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open directory: {ex.Message}", "SimMetryx Observer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenSummary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = _plugin.Engine.LastSummaryMarkdownFile;
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = file,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open summary: {ex.Message}", "SimMetryx Observer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
