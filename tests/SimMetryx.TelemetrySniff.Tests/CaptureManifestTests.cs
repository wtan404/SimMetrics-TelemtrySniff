using System;
using Newtonsoft.Json;
using SimMetryx.TelemetrySniff.Core.Models;
using Xunit;

namespace SimMetryx.TelemetrySniff.Tests
{
    public class CaptureManifestTests
    {
        [Fact]
        public void CaptureManifest_SerializesAndDeserializes_ValidTechnicalMetadata()
        {
            var start = new DateTime(2026, 11, 12, 17, 30, 0, DateTimeKind.Utc);
            var end = start.AddSeconds(60);

            var manifest = new CaptureManifest
            {
                GameName = "AssettoCorsa",
                SessionType = "Hotlap",
                Vehicle = "ks_ferrari_488_gt3",
                Track = "monza",
                StartTimeUtc = start,
                EndTimeUtc = end,
                TotalFramesCaptured = 1800,
                TargetHz = 30,
                RawBufferCaptured = true,
                DynamicPropertiesCaptured = true,
                DiscoveredPropertiesCount = 350,
                JsonlFilePath = @"D:\Projects\SimMetrics-TelemtrySniff\logs\sniff_AssettoCorsa_20261112_173000.jsonl",
                CatalogFilePath = @"D:\Projects\SimMetrics-TelemtrySniff\logs\sniff_AssettoCorsa_20261112_173000.catalog.json"
            };

            var json = manifest.ToJson(indented: true);
            Assert.Contains("\"GameName\": \"AssettoCorsa\"", json);
            Assert.Contains("\"Vehicle\": \"ks_ferrari_488_gt3\"", json);
            Assert.Contains("\"TotalFramesCaptured\": 1800", json);
            Assert.Equal(TimeSpan.FromSeconds(60), manifest.Duration);
            Assert.Equal(30.0, manifest.AverageHz);

            var restored = JsonConvert.DeserializeObject<CaptureManifest>(json);
            Assert.NotNull(restored);
            Assert.Equal("AssettoCorsa", restored!.GameName);
            Assert.Equal("ks_ferrari_488_gt3", restored.Vehicle);
            Assert.Equal(1800, restored.TotalFramesCaptured);
            Assert.Equal(350, restored.DiscoveredPropertiesCount);
            Assert.True(restored.RawBufferCaptured);
            Assert.True(restored.DynamicPropertiesCaptured);
        }
    }
}
