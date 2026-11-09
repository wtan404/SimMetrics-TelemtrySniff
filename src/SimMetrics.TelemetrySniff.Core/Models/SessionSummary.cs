using System;
using System.Text;
using Newtonsoft.Json;

namespace SimMetrics.TelemetrySniff.Core.Models
{
    /// <summary>
    /// High-level session metadata and performance metrics summary designed
    /// to provide AI assistants (such as Antigravity) and engineers with an
    /// instant, human-and-machine-readable overview of a telemetry capture run.
    /// </summary>
    public class SessionSummary
    {
        public string GameName { get; set; } = "Unknown";
        public string SessionType { get; set; } = "Unknown";
        public string CarModel { get; set; } = "Unknown";
        public string TrackCode { get; set; } = "Unknown";

        public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime EndTimeUtc { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration => EndTimeUtc > StartTimeUtc ? EndTimeUtc - StartTimeUtc : TimeSpan.Zero;

        public long TotalFrames { get; set; }
        public int TargetHz { get; set; }
        public double AverageHz => Duration.TotalSeconds > 0 ? Math.Round(TotalFrames / Duration.TotalSeconds, 1) : TargetHz;

        public int CompletedLaps { get; set; }
        public TimeSpan? BestLapTime { get; set; }
        public TimeSpan? LastLapTime { get; set; }

        public double TopSpeedKmh { get; set; }
        public double TopSpeedMph => Math.Round(TopSpeedKmh * 0.621371, 1);
        public double MaxRpm { get; set; }

        public double MinTyreTempC { get; set; }
        public double MaxTyreTempC { get; set; }

        public double FuelStart { get; set; }
        public double FuelEnd { get; set; }
        public double FuelDelta => Math.Round(FuelEnd - FuelStart, 2);

        public int DiscoveredChannelsCount { get; set; }
        public bool RawDataCaptured { get; set; }

        public string? JsonlFilePath { get; set; }
        public string? CatalogFilePath { get; set; }

        public string ToJson(bool indented = true)
        {
            return JsonConvert.SerializeObject(this, indented ? Formatting.Indented : Formatting.None);
        }

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 🏎️ SimMetryx Observer — Telemetry Capture Summary");
            sb.AppendLine();
            sb.AppendLine("> [!NOTE]");
            sb.AppendLine("> This session manifest is formatted for AI assistant analysis (Antigravity), telemetry reverse engineering, and engineering audits.");
            sb.AppendLine();
            sb.AppendLine("## 📋 Session Overview");
            sb.AppendLine($"- **Simulator (Game):** `{GameName}`");
            sb.AppendLine($"- **Session Type:** `{SessionType}`");
            sb.AppendLine($"- **Vehicle:** `{CarModel}`");
            sb.AppendLine($"- **Circuit / Track:** `{TrackCode}`");
            sb.AppendLine($"- **Session Start (UTC):** `{StartTimeUtc:yyyy-MM-dd HH:mm:ss}`");
            sb.AppendLine($"- **Session End (UTC):** `{EndTimeUtc:yyyy-MM-dd HH:mm:ss}`");
            sb.AppendLine($"- **Duration:** `{Duration:hh\\:mm\\:ss\\.ff}`");
            sb.AppendLine($"- **Total Frames Recorded:** `{TotalFrames:N0}` @ ~`{AverageHz}` Hz (Target: `{TargetHz}` Hz)");
            sb.AppendLine();
            sb.AppendLine("## ⏱️ Performance & Dynamics Envelope");
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("| :--- | :--- |");
            sb.AppendLine($"| **Completed Laps** | `{CompletedLaps}` |");
            sb.AppendLine($"| **Best Lap Time** | `{(BestLapTime.HasValue ? BestLapTime.Value.ToString(@"mm\:ss\.fff") : "--:--.---")}` |");
            sb.AppendLine($"| **Last Lap Time** | `{(LastLapTime.HasValue ? LastLapTime.Value.ToString(@"mm\:ss\.fff") : "--:--.---")}` |");
            sb.AppendLine($"| **Top Speed** | `{TopSpeedKmh:F1} km/h` (`{TopSpeedMph:F1} mph`) |");
            sb.AppendLine($"| **Peak RPM** | `{MaxRpm:F0} RPM` |");
            sb.AppendLine($"| **Tyre Temp Range** | `{MinTyreTempC:F1}°C – {MaxTyreTempC:F1}°C` |");
            sb.AppendLine($"| **Fuel (Start / End / Delta)** | `{FuelStart:F1} L` / `{FuelEnd:F1} L` (`{(FuelDelta >= 0 ? "+" : "")}{FuelDelta:F1} L`) |");
            sb.AppendLine();
            sb.AppendLine("## 📊 Schema & Channels");
            sb.AppendLine($"- **Total Discovered Channels:** `{DiscoveredChannelsCount:N0}` properties");
            sb.AppendLine($"- **Game Raw Buffer Captured:** `{(RawDataCaptured ? "Yes" : "No")}`");
            sb.AppendLine();
            sb.AppendLine("## 📁 Storage Artifacts");
            sb.AppendLine($"- **Raw Packet Stream (.jsonl):** `{JsonlFilePath ?? "N/A"}`");
            sb.AppendLine($"- **Channel Schema Catalog (.json):** `{CatalogFilePath ?? "N/A"}`");
            sb.AppendLine();

            return sb.ToString();
        }
    }
}
