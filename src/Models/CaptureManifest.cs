using System;
using Newtonsoft.Json;

namespace SimMetryx.TelemetrySniff.Models
{
    /// <summary>
    /// Developer & AI manifest recording technical metadata for a raw telemetry dump session.
    /// Focuses strictly on telemetry stream information, schemas, and property catalogs for RaceBot integration.
    /// </summary>
    public class CaptureManifest
    {
        public string GameName { get; set; } = "Unknown";
        public string SessionType { get; set; } = "Unknown";
        public string Vehicle { get; set; } = "Unknown";
        public string Track { get; set; } = "Unknown";

        public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime EndTimeUtc { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration => EndTimeUtc > StartTimeUtc ? EndTimeUtc - StartTimeUtc : TimeSpan.Zero;

        public long TotalFramesCaptured { get; set; }
        public int TargetHz { get; set; }
        public double AverageHz => Duration.TotalSeconds > 0 ? Math.Round(TotalFramesCaptured / Duration.TotalSeconds, 1) : TargetHz;

        public bool RawBufferCaptured { get; set; }
        public bool DynamicPropertiesCaptured { get; set; }
        public int DiscoveredPropertiesCount { get; set; }

        public string? JsonlFilePath { get; set; }
        public string? CatalogFilePath { get; set; }

        public string ToJson(bool indented = true)
        {
            return JsonConvert.SerializeObject(this, indented ? Formatting.Indented : Formatting.None);
        }
    }
}
