#if !SIMHUB_REAL
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimHub.Plugins
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginDescriptionAttribute : Attribute
    {
        public string Description { get; }
        public PluginDescriptionAttribute(string description) => Description = description;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class PluginAuthorAttribute : Attribute
    {
        public string Author { get; }
        public PluginAuthorAttribute(string author) => Author = author;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class PluginNameAttribute : Attribute
    {
        public string Name { get; }
        public PluginNameAttribute(string name) => Name = name;
    }

    public interface IPlugin
    {
        void Init(PluginManager pluginManager);
        void End(PluginManager pluginManager);
    }

    public interface IDataPlugin
    {
        void DataUpdate(PluginManager pluginManager, ref GameReaderCommon.GameData data);
    }

    public interface IWPFAddIn
    {
        Control GetWPFSettingsControl(PluginManager pluginManager);
        ImageSource? PictureIcon { get; }
        string LeftMenuTitle { get; }
    }

    public class PluginManager
    {
        public string GameName { get; set; } = "AssettoCorsa";
        private readonly Dictionary<string, object?> _properties = new();

        public void AddProperty(string name, Type type, object? defaultValue) => _properties[name] = defaultValue;
        public void AddAction(string name, Type type, Action<string, string> action) { }
        public void SetPropertyValue(string name, Type type, object? value) => _properties[name] = value;
        public object? GetPropertyValue(string name) => _properties.TryGetValue(name, out var val) ? val : null;
        public IEnumerable<string> GetAllProperties() => _properties.Keys;
    }
}

namespace GameReaderCommon
{
    public class GameData
    {
        public bool GameRunning { get; set; } = true;
        public bool GamePaused { get; set; } = false;
        public string SessionTypeName { get; set; } = "Practice";
        public StatusData NewData { get; set; } = new StatusData();
    }

    public class StatusData
    {
        public string CarModel { get; set; } = string.Empty;
        public string TrackCode { get; set; } = string.Empty;
        public double SpeedKmh { get; set; }
        public double Rpms { get; set; }
        public double MaxRpm { get; set; }
        public string? Gear { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double Clutch { get; set; }
        public double SteeringAngle { get; set; }
        public double OrientationPitch { get; set; }
        public double OrientationRoll { get; set; }
        public double OrientationYaw { get; set; }
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

        public double TyreTemperatureFrontLeft { get; set; }
        public double TyreTemperatureFrontRight { get; set; }
        public double TyreTemperatureRearLeft { get; set; }
        public double TyreTemperatureRearRight { get; set; }

        public double TyrePressureFrontLeft { get; set; }
        public double TyrePressureFrontRight { get; set; }
        public double TyrePressureRearLeft { get; set; }
        public double TyrePressureRearRight { get; set; }

        public double TyreWearFrontLeft { get; set; }
        public double TyreWearFrontRight { get; set; }
        public double TyreWearRearLeft { get; set; }
        public double TyreWearRearRight { get; set; }

        public virtual object? GetRawDataObject() => null;
    }
}
#endif
