using System;
using System.IO;
using System.Text;
using System.Threading;
using SimMetryx.TelemetrySniff.Core.AssettoCorsa;
using SimMetryx.TelemetrySniff.Core.Models;
using SimMetryx.TelemetrySniff.Core.Storage;
using Xunit;

namespace SimMetryx.TelemetrySniff.Tests
{
    public class AssettoCorsaTests
    {
        [Fact]
        public void Parse_ValidHandshakeResponse_ExtractsMetadata()
        {
            var buffer = new byte[408];
            Encoding.Unicode.GetBytes("ks_ferrari_488_gt3").CopyTo(buffer, 0);
            Encoding.Unicode.GetBytes("Michael Schumacher").CopyTo(buffer, 100);
            BitConverter.GetBytes(42).CopyTo(buffer, 200);
            BitConverter.GetBytes(1).CopyTo(buffer, 204);
            Encoding.Unicode.GetBytes("spa").CopyTo(buffer, 208);
            Encoding.Unicode.GetBytes("gp").CopyTo(buffer, 308);

            var hs = AcHandshakeResponse.Parse(buffer);

            Assert.NotNull(hs);
            Assert.Equal("ks_ferrari_488_gt3", hs.CarName);
            Assert.Equal("Michael Schumacher", hs.DriverName);
            Assert.Equal(42, hs.Identifier);
            Assert.Equal(1, hs.Version);
            Assert.Equal("spa", hs.TrackName);
            Assert.Equal("gp", hs.TrackConfig);
        }

        [Fact]
        public void Parse_ValidCarInfo_ExtractsPhysicsData()
        {
            var buffer = new byte[350];
            buffer[0] = (byte)'a';
            int offset = 1;

            BitConverter.GetBytes(340).CopyTo(buffer, offset); offset += 4; // size
            BitConverter.GetBytes(180.5f).CopyTo(buffer, offset); offset += 4; // speed kmh
            BitConverter.GetBytes(112.1f).CopyTo(buffer, offset); offset += 4; // speed mph
            BitConverter.GetBytes(50.1f).CopyTo(buffer, offset); offset += 4; // speed ms

            buffer[offset++] = 1; // isAbsEnabled
            buffer[offset++] = 0; // isAbsInAction
            buffer[offset++] = 1; // isTcInAction
            buffer[offset++] = 1; // isTcEnabled
            buffer[offset++] = 0; // isInPit
            buffer[offset++] = 0; // isEngineLimiterOn

            offset += 2; // padding

            BitConverter.GetBytes(0.1f).CopyTo(buffer, offset); offset += 4; // accG vertical
            BitConverter.GetBytes(0.85f).CopyTo(buffer, offset); offset += 4; // accG horizontal
            BitConverter.GetBytes(0.42f).CopyTo(buffer, offset); offset += 4; // accG frontal

            BitConverter.GetBytes(83450).CopyTo(buffer, offset); offset += 4; // lapTimeMs
            BitConverter.GetBytes(82900).CopyTo(buffer, offset); offset += 4; // lastLapMs
            BitConverter.GetBytes(81500).CopyTo(buffer, offset); offset += 4; // bestLapMs
            BitConverter.GetBytes(4).CopyTo(buffer, offset); offset += 4; // lapCount

            BitConverter.GetBytes(0.95f).CopyTo(buffer, offset); offset += 4; // gas
            BitConverter.GetBytes(0.0f).CopyTo(buffer, offset); offset += 4; // brake
            BitConverter.GetBytes(0.0f).CopyTo(buffer, offset); offset += 4; // clutch
            BitConverter.GetBytes(7450f).CopyTo(buffer, offset); offset += 4; // engineRPM
            BitConverter.GetBytes(-0.15f).CopyTo(buffer, offset); offset += 4; // steer
            BitConverter.GetBytes(4).CopyTo(buffer, offset); offset += 4; // gear (4 => 3rd gear)
            BitConverter.GetBytes(0.35f).CopyTo(buffer, offset); offset += 4; // cgHeight

            var car = AcCarInfo.Parse(buffer);

            Assert.NotNull(car);
            Assert.Equal('a', car.Identifier);
            Assert.Equal(180.5f, car.SpeedKmh, 1);
            Assert.True(car.IsAbsEnabled);
            Assert.False(car.IsAbsInAction);
            Assert.True(car.IsTcInAction);
            Assert.Equal(0.85f, car.AccGHorizontal, 2);
            Assert.Equal(83450, car.LapTimeMs);
            Assert.Equal(4, car.LapCount);
            Assert.Equal(0.95f, car.Gas, 2);
            Assert.Equal(7450f, car.EngineRpm, 1);
            Assert.Equal(4, car.Gear);
        }

        [Fact]
        public void TelemetrySessionWriter_CreatesJsonlAndCatalog()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SimMetryx_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                using var writer = new TelemetrySessionWriter(tempDir);
                writer.Start("AssettoCorsa");

                var sample = new TelemetryPacket
                {
                    CarModel = "ks_ferrari_488_gt3",
                    DriverName = "Driver Test",
                    Normalized = new NormalizedData
                    {
                        SpeedKmh = 150.0,
                        Rpms = 6500,
                        Throttle = 1.0,
                        Gear = "3"
                    }
                };

                writer.Enqueue(sample);
                Thread.Sleep(100);
                writer.Stop(sample);

                Assert.NotNull(writer.CurrentSessionFile);
                Assert.NotNull(writer.CurrentCatalogFile);
                Assert.True(File.Exists(writer.CurrentSessionFile));
                Assert.True(File.Exists(writer.CurrentCatalogFile));

                var jsonlContent = File.ReadAllText(writer.CurrentSessionFile);
                Assert.Contains("ks_ferrari_488_gt3", jsonlContent);

                var catalogContent = File.ReadAllText(writer.CurrentCatalogFile);
                Assert.Contains("Normalized.SpeedKmh", catalogContent);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
