using System;
using Newtonsoft.Json;
using SimMetryx.TelemetrySniff.Models;
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

        [Fact]
        public void TelemetryPacket_SerializesAndDeserializes_UniversalFormat()
        {
            var packet = new TelemetryPacket
            {
                TimestampUtc = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc),
                GameName = "Automobilista2",
                GameRunning = true,
                GamePaused = false,
                SessionType = "Race",
                CarModel = "Porsche 911 GT3 R",
                TrackCode = "Spa",
                Normalized = new NormalizedData
                {
                    SpeedKmh = 245.8,
                    Rpms = 8200,
                    MaxRpm = 9000,
                    Gear = "5",
                    Throttle = 1.0,
                    Brake = 0.0,
                    Fuel = 42.5,
                    Tyres = new TyreData
                    {
                        TemperaturesC = new[] { 85.0, 86.2, 84.1, 85.5 },
                        PressuresKpa = new[] { 172.0, 175.0, 168.0, 170.0 },
                        PressuresPsi = new[] { 25.0, 25.4, 24.4, 24.7 },
                        WearPercent = new[] { 98.0, 97.5, 99.0, 98.8 }
                    }
                },
                Properties = new System.Collections.Generic.Dictionary<string, object?>
                {
                    ["EngineWaterTemp"] = 92.5,
                    ["DrsAvailable"] = false
                }
            };

            var json = JsonConvert.SerializeObject(packet);
            Assert.Contains("\"gameName\":\"Automobilista2\"", json);
            Assert.Contains("\"SpeedKmh\":245.8", json);

            var restored = JsonConvert.DeserializeObject<TelemetryPacket>(json);
            Assert.NotNull(restored);
            Assert.Equal("Automobilista2", restored!.GameName);
            Assert.Equal(245.8, restored.Normalized.SpeedKmh);
            Assert.Equal(172.0, restored.Normalized.Tyres.PressuresKpa[0]);
            Assert.Equal(25.0, restored.Normalized.Tyres.PressuresPsi[0]);
            Assert.NotNull(restored.Properties);
            Assert.True(restored.Properties.ContainsKey("EngineWaterTemp"));
        }
    }
}
