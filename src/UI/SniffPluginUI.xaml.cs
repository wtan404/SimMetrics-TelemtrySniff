using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SimMetryx.TelemetrySniff.UI
{
    public partial class SniffPluginUI : UserControl
    {
        private readonly TelemetrySniffPlugin _plugin;
        private readonly DispatcherTimer _uiTimer;
        private bool _isInitialized = false;

        public SniffPluginUI(TelemetrySniffPlugin plugin)
        {
            _plugin = plugin;
            InitializeComponent();

            TxtOutputDir.Text = _plugin.Engine.OutputDirectory;
            SliderHz.Value = _plugin.Engine.TargetHz;
            ChkRawPayload.IsChecked = _plugin.Engine.CaptureRawPayload;
            ChkDynamicProps.IsChecked = _plugin.Engine.CaptureAllProperties;
            _isInitialized = true;

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
            var isMasterEnabled = _plugin.Engine.IsEnabled;

            // Master Power Toggle Display
            if (isMasterEnabled)
            {
                BtnMasterPower.Content = "🟢 OBSERVER: ACTIVE";
                BtnMasterPower.Background = new SolidColorBrush(Color.FromRgb(46, 139, 87));
                BtnToggleCapture.IsEnabled = true;
            }
            else
            {
                BtnMasterPower.Content = "⏸️ OBSERVER: DISABLED";
                BtnMasterPower.Background = new SolidColorBrush(Color.FromRgb(140, 40, 40));
                BadgeIndicator.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                TxtBadgeStatus.Text = "DISABLED";
                TxtGameName.Text = "Observer Off (Control Panel)";
                TxtSessionDetail.Text = "Master switch is turned OFF. Telemetry sniffing is halted.";
                BtnToggleCapture.IsEnabled = false;
                BtnToggleCapture.Content = "⏸️ OBSERVER DISABLED";
                BtnToggleCapture.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50));
                UpdateArtifactLinks();
                return;
            }

            var pm = _plugin.PluginManager;
            var isRunning = pm?.GetPropertyValue("IsGameRunning") is bool running && running;
            var activeGame = pm?.GetPropertyValue("ActiveGame") as string;
            var sessionType = pm?.GetPropertyValue("SessionType") as string ?? "Unknown";
            var car = pm?.GetPropertyValue("CurrentVehicle") as string ?? "Unknown Car";
            var track = pm?.GetPropertyValue("CurrentTrack") as string ?? "Unknown Track";

            // Update Simulator Title & Detail (strictly bound to physical process running)
            if (isRunning && !string.IsNullOrEmpty(activeGame) && activeGame != "None")
            {
                TxtGameName.Text = activeGame;
                TxtSessionDetail.Text = $"{sessionType} • {track} • {car}";
            }
            else
            {
                TxtGameName.Text = "No Game Running (Standby)";
                TxtSessionDetail.Text = _plugin.Engine.IsArmed
                    ? "🟡 Observer is armed! Waiting for simulator process to launch..."
                    : "Waiting for simulator session launch...";
            }

            // Update Badge & Action Button State
            if (_plugin.Engine.IsCapturing)
            {
                BadgeIndicator.Background = new SolidColorBrush(Color.FromRgb(220, 50, 50));
                TxtBadgeStatus.Text = "RECORDING";

                BtnToggleCapture.Content = "⏹️ STOP RAW CAPTURE";
                BtnToggleCapture.Background = new SolidColorBrush(Color.FromRgb(180, 40, 40));
            }
            else if (_plugin.Engine.IsArmed)
            {
                BadgeIndicator.Background = new SolidColorBrush(Color.FromRgb(218, 165, 32));
                TxtBadgeStatus.Text = "ARMED";

                BtnToggleCapture.Content = "❌ DISARM OBSERVER";
                BtnToggleCapture.Background = new SolidColorBrush(Color.FromRgb(160, 110, 20));
            }
            else if (isRunning)
            {
                BadgeIndicator.Background = new SolidColorBrush(Color.FromRgb(46, 139, 87));
                TxtBadgeStatus.Text = "SIM RUNNING";

                BtnToggleCapture.Content = "▶️ START RAW CAPTURE";
                BtnToggleCapture.Background = new SolidColorBrush(Color.FromRgb(14, 112, 166));
            }
            else
            {
                BadgeIndicator.Background = new SolidColorBrush(Color.FromRgb(90, 90, 90));
                TxtBadgeStatus.Text = "STANDBY";

                BtnToggleCapture.Content = "⏳ ARM OBSERVER (WAIT FOR SIM)";
                BtnToggleCapture.Background = new SolidColorBrush(Color.FromRgb(40, 90, 130));
            }

            TxtFrames.Text = _plugin.Engine.FramesCaptured.ToString("N0");
            TxtPropertiesCount.Text = _plugin.Engine.DiscoveredPropertyCount.ToString("N0");

            UpdateArtifactLinks();
        }

        private void UpdateArtifactLinks()
        {
            var lastJsonl = _plugin.Engine.CurrentSessionFile;
            if (!string.IsNullOrEmpty(lastJsonl) && File.Exists(lastJsonl))
            {
                TxtLastJsonl.Text = lastJsonl;
                BtnOpenJsonl.IsEnabled = true;
            }
            else
            {
                TxtLastJsonl.Text = "No raw capture completed yet";
                BtnOpenJsonl.IsEnabled = false;
            }

            var lastCatalog = _plugin.Engine.LastCatalogFile;
            if (!string.IsNullOrEmpty(lastCatalog) && File.Exists(lastCatalog))
            {
                TxtLastCatalog.Text = lastCatalog;
                BtnOpenCatalog.IsEnabled = true;
            }
            else
            {
                TxtLastCatalog.Text = "No channel catalog completed yet";
                BtnOpenCatalog.IsEnabled = false;
            }
        }

        private void BtnMasterPower_Click(object sender, RoutedEventArgs e)
        {
            _plugin.Engine.IsEnabled = !_plugin.Engine.IsEnabled;
        }

        private void BtnToggleCapture_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ToggleCapture();
        }

        private void SliderHz_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || TxtHz == null) return;
            var hz = (int)e.NewValue;
            TxtHz.Text = $"{hz} Hz";
            if (_plugin?.Engine != null)
            {
                _plugin.Engine.TargetHz = hz;
            }
        }

        private void ChkConfig_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _plugin?.Engine == null) return;
            if (ChkRawPayload != null)
                _plugin.Engine.CaptureRawPayload = ChkRawPayload.IsChecked == true;
            if (ChkDynamicProps != null)
                _plugin.Engine.CaptureAllProperties = ChkDynamicProps.IsChecked == true;
        }

        private void TxtOutputDir_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyOutputDirectory(TxtOutputDir.Text);
        }

        private void TxtOutputDir_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyOutputDirectory(TxtOutputDir.Text);
                Keyboard.ClearFocus();
            }
        }

        private void ApplyOutputDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                path = path.Trim();
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                _plugin.Engine.OutputDirectory = path;
                TxtOutputDir.Text = path;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not create or access directory '{path}':\n{ex.Message}", "SimMetryx Observer", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtOutputDir.Text = _plugin.Engine.OutputDirectory;
            }
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "Select SimMetryx Observer Logs & Telemetry Dumps Directory";
                dialog.SelectedPath = Directory.Exists(_plugin.Engine.OutputDirectory)
                    ? _plugin.Engine.OutputDirectory
                    : @"D:\Projects\SimMetrics-TelemtrySniff\logs";
                dialog.ShowNewFolderButton = true;

                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    ApplyOutputDirectory(dialog.SelectedPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening folder browser: {ex.Message}", "SimMetryx Observer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void BtnOpenJsonl_Click(object sender, RoutedEventArgs e)
        {
            OpenFileInExplorer(_plugin.Engine.CurrentSessionFile);
        }

        private void BtnOpenCatalog_Click(object sender, RoutedEventArgs e)
        {
            OpenFileInExplorer(_plugin.Engine.LastCatalogFile);
        }

        private void OpenFileInExplorer(string? filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file: {ex.Message}", "SimMetryx Observer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
