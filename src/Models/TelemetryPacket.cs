using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SimMetrics.TelemetrySniff.Models
{
    /// <summary>
    /// Represents a discrete telemetry capture frame containing normalized metrics,
    /// dynamic SimHub property registry values, and game-specific raw data payloads.
    /// </summary>
    public class TelemetryPacket
    {
        [JsonProperty("timestampUtc")]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        [JsonProperty("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonProperty("gameRunning")]
        public bool GameRunning { get; set; }

        [JsonProperty("gamePaused")]
        public bool GamePaused { get; set; }

        [JsonProperty("sessionType")]
        public string? SessionType { get; set; }

        [JsonProperty("carModel")]
        public string? CarModel { get; set; }

        [JsonProperty("trackCode")]
        public string? TrackCode { get; set; }

        [JsonProperty("normalized")]
        public NormalizedData Normalized { get; set; } = new NormalizedData();

        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object?>? Properties { get; set; }

        [JsonProperty("rawPayload", NullValueHandling = NullValueHandling.Ignore)]
        public object? RawPayload { get; set; }
    }

    /// <summary>
    /// Baseline normalized telemetry channels extracted from SimHub's GameData structure.
    /// </summary>
    public class NormalizedData
    {
        public double SpeedKmh { get; set; }
        public double Rpms { get; set; }
        public double MaxRpm { get; set; }
        public string? Gear { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double Clutch { get; set; }
        public double SteeringAngle { get; set; }

        public double Pitch { get; set; }
        public double Roll { get; set; }
        public double Yaw { get; set; }

        public double AccelerationSurge { get; set; }
        public double AccelerationSway { get; set; }
        public double AccelerationHeave { get; set; }

        public int CompletedLaps { get; set; }
        public TimeSpan? CurrentLapTime { get; set; }
        public TimeSpan? BestLapTime { get; set; }
        public TimeSpan? LastLapTime { get; set; }
        public double TrackPositionPercent { get; set; }

        public double Fuel { get; set; }
        public double MaxFuel { get; set; }

        public TyreData Tyres { get; set; } = new TyreData();
    }

    /// <summary>
    /// Normalized wheel & tyre information across Front-Left, Front-Right, Rear-Left, Rear-Right.
    /// </summary>
    public class TyreData
    {
        public double[] TemperaturesC { get; set; } = new double[4];
        public double[] PressuresKpa { get; set; } = new double[4];
        public double[] WearPercent { get; set; } = new double[4];
    }

    /// <summary>
    /// Metadata descriptor for discovered SimHub properties.
    /// </summary>
    public class PropertyDescriptor
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public object? SampleValue { get; set; }
    }
}
