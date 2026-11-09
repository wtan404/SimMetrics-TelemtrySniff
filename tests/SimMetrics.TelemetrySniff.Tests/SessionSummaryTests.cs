using System;
using Newtonsoft.Json;
using SimMetrics.TelemetrySniff.Core.Models;
using Xunit;

namespace SimMetrics.TelemetrySniff.Tests
{
    public class SessionSummaryTests
    {
        [Fact]
        public void SessionSummary_CalculatesDerivedMetrics_Correctly()
        {
            var start = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
            var end = start.AddSeconds(100);

            var summary = new SessionSummary
            {
                GameName = "Automobilista2",
                SessionType = "TIME_ATTACK",
                CarModel = "Porsche 911 GT3 R",
                TrackCode = "Spa-Francorchamps",
                StartTimeUtc = start,
                EndTimeUtc = end,
                TotalFrames = 3000,
                TargetHz = 30,
                TopSpeedKmh = 270.0,
                FuelStart = 50.0,
                FuelEnd = 45.5,
                CompletedLaps = 2,
                BestLapTime = TimeSpan.FromSeconds(136.482),
                LastLapTime = TimeSpan.FromSeconds(136.482),
                MinTyreTempC = 78.5,
                MaxTyreTempC = 92.3,
                DiscoveredChannelsCount = 250,
                RawDataCaptured = true,
                JsonlFilePath = @"C:\SimMetrics\sniff.jsonl",
                CatalogFilePath = @"C:\SimMetrics\sniff.catalog.json"
            };

            Assert.Equal(TimeSpan.FromSeconds(100), summary.Duration);
            Assert.Equal(30.0, summary.AverageHz);
            Assert.Equal(Math.Round(270.0 * 0.621371, 1), summary.TopSpeedMph);
            Assert.Equal(-4.5, summary.FuelDelta);
        }

        [Fact]
        public void SessionSummary_GeneratesReadableMarkdownManifest()
        {
            var summary = new SessionSummary
            {
                GameName = "AssettoCorsa",
                SessionType = "Practice",
                CarModel = "Ferrari 488 GT3",
                TrackCode = "Monza",
                StartTimeUtc = new DateTime(2026, 11, 8, 14, 0, 0, DateTimeKind.Utc),
                EndTimeUtc = new DateTime(2026, 11, 8, 14, 10, 0, DateTimeKind.Utc),
                TotalFrames = 18000,
                TargetHz = 30,
                TopSpeedKmh = 285.5,
                CompletedLaps = 5,
                BestLapTime = TimeSpan.FromMinutes(1.78),
                DiscoveredChannelsCount = 180,
                RawDataCaptured = true
            };

            var markdown = summary.ToMarkdown();

            Assert.Contains("SimMetryx Observer — Telemetry Capture Summary", markdown);
            Assert.Contains("`AssettoCorsa`", markdown);
            Assert.Contains("`Ferrari 488 GT3`", markdown);
            Assert.Contains("`Monza`", markdown);
            Assert.Contains("285.5 km/h", markdown);
            Assert.Contains("Completed Laps", markdown);
            Assert.Contains("`180` properties", markdown);
        }

        [Fact]
        public void SessionSummary_SerializesAndDeserializes_ValidJson()
        {
            var original = new SessionSummary
            {
                GameName = "iRacing",
                SessionType = "Race",
                CarModel = "Dallara F3",
                TrackCode = "Silverstone",
                TotalFrames = 500,
                TopSpeedKmh = 240.2
            };

            var json = original.ToJson();
            var restored = JsonConvert.DeserializeObject<SessionSummary>(json);

            Assert.NotNull(restored);
            Assert.Equal("iRacing", restored!.GameName);
            Assert.Equal("Dallara F3", restored.CarModel);
            Assert.Equal(240.2, restored.TopSpeedKmh);
        }
    }
}
