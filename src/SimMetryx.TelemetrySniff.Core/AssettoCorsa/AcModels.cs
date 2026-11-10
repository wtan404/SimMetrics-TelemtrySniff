using System;
using System.Text;
using Newtonsoft.Json;

namespace SimMetryx.TelemetrySniff.Core.AssettoCorsa
{
    public enum AcOperationId
    {
        Handshake = 0,
        SubscribeUpdate = 1,
        SubscribeSpot = 2,
        Dismiss = 3
    }

    /// <summary>
    /// Decoded initial handshake response from Assetto Corsa UDP server.
    /// </summary>
    public class AcHandshakeResponse
    {
        [JsonProperty("carName")]
        public string CarName { get; set; } = string.Empty;

        [JsonProperty("driverName")]
        public string DriverName { get; set; } = string.Empty;

        [JsonProperty("identifier")]
        public int Identifier { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("trackName")]
        public string TrackName { get; set; } = string.Empty;

        [JsonProperty("trackConfig")]
        public string TrackConfig { get; set; } = string.Empty;

        public static AcHandshakeResponse? Parse(byte[] data)
        {
            if (data == null || data.Length < 408)
                return null;

            try
            {
                var response = new AcHandshakeResponse
                {
                    CarName = ReadUnicodeString(data, 0, 100),
                    DriverName = ReadUnicodeString(data, 100, 100),
                    Identifier = BitConverter.ToInt32(data, 200),
                    Version = BitConverter.ToInt32(data, 204),
                    TrackName = ReadUnicodeString(data, 208, 100),
                    TrackConfig = ReadUnicodeString(data, 308, 100)
                };
                return response;
            }
            catch
            {
                return null;
            }
        }

        private static string ReadUnicodeString(byte[] buffer, int offset, int length)
        {
            var raw = Encoding.Unicode.GetString(buffer, offset, length);
            var nullIdx = raw.IndexOf('\0');
            return nullIdx >= 0 ? raw.Substring(0, nullIdx).Trim() : raw.Trim();
        }
    }

    /// <summary>
    /// Real-time car physics telemetry packet streamed from Assetto Corsa.
    /// </summary>
    public class AcCarInfo
    {
        public char Identifier { get; set; }
        public int Size { get; set; }
        public float SpeedKmh { get; set; }
        public float SpeedMph { get; set; }
        public float SpeedMs { get; set; }

        public bool IsAbsEnabled { get; set; }
        public bool IsAbsInAction { get; set; }
        public bool IsTcInAction { get; set; }
        public bool IsTcEnabled { get; set; }
        public bool IsInPit { get; set; }
        public bool IsEngineLimiterOn { get; set; }

        public float AccGVertical { get; set; }
        public float AccGHorizontal { get; set; }
        public float AccGFrontal { get; set; }

        public int LapTimeMs { get; set; }
        public int LastLapMs { get; set; }
        public int BestLapMs { get; set; }
        public int LapCount { get; set; }

        public float Gas { get; set; }
        public float Brake { get; set; }
        public float Clutch { get; set; }
        public float EngineRpm { get; set; }
        public float Steer { get; set; }
        public int Gear { get; set; }
        public float CgHeight { get; set; }

        // 4 wheels (FL=0, FR=1, RL=2, RR=3)
        public float[] WheelAngularSpeed { get; set; } = new float[4];
        public float[] SlipAngle { get; set; } = new float[4];
        public float[] SlipAngleContactPatch { get; set; } = new float[4];
        public float[] SlipRatio { get; set; } = new float[4];
        public float[] TyreSlip { get; set; } = new float[4];
        public float[] NdSlip { get; set; } = new float[4];
        public float[] Load { get; set; } = new float[4];
        public float[] Dy { get; set; } = new float[4];
        public float[] Mz { get; set; } = new float[4];
        public float[] TyreDirtyLevel { get; set; } = new float[4];
        public float[] CamberRad { get; set; } = new float[4];
        public float[] TyreRadius { get; set; } = new float[4];
        public float[] TyreLoadedRadius { get; set; } = new float[4];
        public float[] SuspensionHeight { get; set; } = new float[4];

        public float CarPositionNormalized { get; set; }
        public float CarSlope { get; set; }
        public float[] CarCoordinates { get; set; } = new float[3];

        public static AcCarInfo? Parse(byte[] data)
        {
            if (data == null || data.Length < 100)
                return null;

            try
            {
                var info = new AcCarInfo
                {
                    Identifier = (char)data[0]
                };

                int offset = 1;
                info.Size = ReadInt32(data, ref offset);
                info.SpeedKmh = ReadSingle(data, ref offset);
                info.SpeedMph = ReadSingle(data, ref offset);
                info.SpeedMs = ReadSingle(data, ref offset);

                info.IsAbsEnabled = ReadBool(data, ref offset);
                info.IsAbsInAction = ReadBool(data, ref offset);
                info.IsTcInAction = ReadBool(data, ref offset);
                info.IsTcEnabled = ReadBool(data, ref offset);
                info.IsInPit = ReadBool(data, ref offset);
                info.IsEngineLimiterOn = ReadBool(data, ref offset);

                // Struct padding to 4-byte boundary
                offset += 2;

                info.AccGVertical = ReadSingle(data, ref offset);
                info.AccGHorizontal = ReadSingle(data, ref offset);
                info.AccGFrontal = ReadSingle(data, ref offset);

                info.LapTimeMs = ReadInt32(data, ref offset);
                info.LastLapMs = ReadInt32(data, ref offset);
                info.BestLapMs = ReadInt32(data, ref offset);
                info.LapCount = ReadInt32(data, ref offset);

                info.Gas = ReadSingle(data, ref offset);
                info.Brake = ReadSingle(data, ref offset);
                info.Clutch = ReadSingle(data, ref offset);
                info.EngineRpm = ReadSingle(data, ref offset);
                info.Steer = ReadSingle(data, ref offset);
                info.Gear = ReadInt32(data, ref offset);
                info.CgHeight = ReadSingle(data, ref offset);

                if (offset + 16 <= data.Length) info.WheelAngularSpeed = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.SlipAngle = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.SlipAngleContactPatch = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.SlipRatio = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.TyreSlip = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.NdSlip = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.Load = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.Dy = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.Mz = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.TyreDirtyLevel = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.CamberRad = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.TyreRadius = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.TyreLoadedRadius = ReadFloatArray(data, ref offset, 4);
                if (offset + 16 <= data.Length) info.SuspensionHeight = ReadFloatArray(data, ref offset, 4);

                if (offset + 4 <= data.Length) info.CarPositionNormalized = ReadSingle(data, ref offset);
                if (offset + 4 <= data.Length) info.CarSlope = ReadSingle(data, ref offset);
                if (offset + 12 <= data.Length) info.CarCoordinates = ReadFloatArray(data, ref offset, 3);

                return info;
            }
            catch
            {
                return null;
            }
        }

        private static int ReadInt32(byte[] b, ref int offset)
        {
            var val = BitConverter.ToInt32(b, offset);
            offset += 4;
            return val;
        }

        private static float ReadSingle(byte[] b, ref int offset)
        {
            var val = BitConverter.ToSingle(b, offset);
            offset += 4;
            return val;
        }

        private static bool ReadBool(byte[] b, ref int offset)
        {
            var val = b[offset] != 0;
            offset += 1;
            return val;
        }

        private static float[] ReadFloatArray(byte[] b, ref int offset, int count)
        {
            var arr = new float[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = BitConverter.ToSingle(b, offset);
                offset += 4;
            }
            return arr;
        }
    }

    /// <summary>
    /// Lap completion record packet streamed from Assetto Corsa.
    /// </summary>
    public class AcLapInfo
    {
        public char Identifier { get; set; }
        public int Size { get; set; }
        public int LapTimeMs { get; set; }
        public int CarIdentifierNumber { get; set; }
        public int Lap { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string CarName { get; set; } = string.Empty;
        public int TimeMs { get; set; }

        public static AcLapInfo? Parse(byte[] data)
        {
            if (data == null || data.Length < 220)
                return null;

            try
            {
                var lap = new AcLapInfo
                {
                    Identifier = (char)data[0],
                    Size = BitConverter.ToInt32(data, 1),
                    LapTimeMs = BitConverter.ToInt32(data, 5),
                    CarIdentifierNumber = BitConverter.ToInt32(data, 9),
                    Lap = BitConverter.ToInt32(data, 13),
                    DriverName = ReadUnicode(data, 17, 100),
                    CarName = ReadUnicode(data, 117, 100),
                    TimeMs = BitConverter.ToInt32(data, 217)
                };
                return lap;
            }
            catch
            {
                return null;
            }
        }

        private static string ReadUnicode(byte[] b, int offset, int length)
        {
            var str = Encoding.Unicode.GetString(b, offset, length);
            var nullIdx = str.IndexOf('\0');
            return nullIdx >= 0 ? str.Substring(0, nullIdx).Trim() : str.Trim();
        }
    }
}
