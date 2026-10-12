using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SimMetrics.TelemetrySniff.UI
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
            var gameName = _plugin.PluginManager?.GameName;
            TxtGameName.Text = string.IsNullOrEmpty(gameName) ? "None (SimHub idle)" : gameName;

            if (_plugin.Engine.IsCapturing)
            {
                TxtStatus.Text = "Capturing...";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Orange;
                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = true;
            }
            else
            {
                TxtStatus.Text = "Idle";
                TxtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
            }

            TxtFrames.Text = _plugin.Engine.FramesCaptured.ToString("N0");
            TxtPropertiesCount.Text = _plugin.Engine.DiscoveredPropertyCount.ToString("N0");
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _plugin.StartCapture();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _plugin.StopCapture();
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
                MessageBox.Show($"Could not open directory: {ex.Message}", "SimMetrics", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
