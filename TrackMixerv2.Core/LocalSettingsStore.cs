using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TrackMixerv2;

public static class LocalSettingsStore
{
    public static class Keys
    {
        public const string RecentVideosJson = "RecentVideosJson";
        public const string SuppressRootFolderPrompt = "SuppressRootFolderPrompt";
        public const string DoubleClickOnNewTab = "DoubleClickOnNewTab";
        public const string DragAndDropOnNewTab = "DragAndDropOnNewTab";
        public const string RecentVideo = "RecentVideo";
        public const string SkipSeconds = "SkipSeconds";
        public const string SliderWheelSpeed = "SliderWheelSpeed";
        public const string SpeedBoostRate = "SpeedBoostRate";
        public const string SpeedSlowRate = "SpeedSlowRate";
        public const string TransportRatesJson = "TransportRatesJson";
    }

    public const int DefaultSkipSeconds = 5;
    public const int DefaultSliderWheelSpeed = 2;
    public const double DefaultSpeedBoostRate = 2.0;
    public const double DefaultSpeedSlowRate = 0.25;

    private static readonly object Lock = new();
    private static Dictionary<string, object>? cache;

    public const string JsonPathEnvVar = "TRACKMIXER_LOCAL_SETTINGS_PATH";

    public static string JsonPath =>
        AppPaths.ResolveDataFilePath(
            Environment.GetEnvironmentVariable(JsonPathEnvVar),
            "local_settings.json");

    public static void ResetCache()
    {
        lock (Lock)
            cache = null;
    }

    public static bool ContainsKey(string key)
    {
        lock (Lock)
            return GetOrLoadCacheLocked().ContainsKey(key);
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        if (!TryGetValue(key, out object? value) || value == null)
            return defaultValue;

        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out bool parsed) => parsed,
            _ => defaultValue,
        };
    }

    public static string? GetString(string key, string? defaultValue = null)
    {
        if (!TryGetValue(key, out object? value) || value == null)
            return defaultValue;

        return value switch
        {
            string s => s,
            _ => value.ToString(),
        };
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        if (!TryGetValue(key, out object? value) || value == null)
            return defaultValue;

        return value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out int parsed) => parsed,
            _ => defaultValue,
        };
    }

    public static int GetSkipSeconds(int defaultValue = DefaultSkipSeconds) =>
        Math.Max(1, GetInt(Keys.SkipSeconds, defaultValue));

    public static void SetSkipSeconds(int seconds) =>
        Set(Keys.SkipSeconds, Math.Max(1, seconds));

    public static int GetSliderWheelSpeed(int defaultValue = DefaultSliderWheelSpeed) =>
        Math.Max(1, GetInt(Keys.SliderWheelSpeed, defaultValue));

    public static void SetSliderWheelSpeed(int speed) =>
        Set(Keys.SliderWheelSpeed, Math.Max(1, speed));

    public static double GetSpeedBoostRate(double defaultValue = DefaultSpeedBoostRate) =>
        PlaybackRates.NormalizeBoostRate(GetDouble(Keys.SpeedBoostRate, defaultValue));

    public static void SetSpeedBoostRate(double rate) =>
        Set(Keys.SpeedBoostRate, PlaybackRates.NormalizeBoostRate(rate));

    public static double GetSpeedSlowRate(double defaultValue = DefaultSpeedSlowRate) =>
        PlaybackRates.NormalizeSlowRate(GetDouble(Keys.SpeedSlowRate, defaultValue));

    public static void SetSpeedSlowRate(double rate) =>
        Set(Keys.SpeedSlowRate, PlaybackRates.NormalizeSlowRate(rate));

    public static IReadOnlyList<double> GetTransportRates()
    {
        if (!ContainsKey(Keys.TransportRatesJson))
            return PlaybackRates.Defaults.ToArray();

        try
        {
            string? json = GetString(Keys.TransportRatesJson);
            if (string.IsNullOrWhiteSpace(json))
                return PlaybackRates.Defaults.ToArray();

            var parsed = JsonConvert.DeserializeObject<List<double>>(json);
            return PlaybackRates.SanitizeTransportRates(parsed);
        }
        catch
        {
            return PlaybackRates.Defaults.ToArray();
        }
    }

    public static void SetTransportRates(IEnumerable<double> rates)
    {
        IReadOnlyList<double> sanitized = PlaybackRates.SanitizeTransportRates(rates);
        Set(Keys.TransportRatesJson, JsonConvert.SerializeObject(sanitized));
    }

    public static void ResetTransportRates()
    {
        lock (Lock)
        {
            GetOrLoadCacheLocked().Remove(Keys.TransportRatesJson);
            PersistLocked();
        }
    }

    public static double GetDouble(string key, double defaultValue = 0)
    {
        if (!TryGetValue(key, out object? value) || value == null)
            return defaultValue;

        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            string s when double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => defaultValue,
        };
    }

    public static void SetBool(string key, bool value) => Set(key, value);

    public static void SetString(string key, string value) => Set(key, value);

    public static void Set(string key, object value)
    {
        lock (Lock)
        {
            GetOrLoadCacheLocked()[key] = value;
            PersistLocked();
        }
    }

    private static bool TryGetValue(string key, out object? value)
    {
        lock (Lock)
            return GetOrLoadCacheLocked().TryGetValue(key, out value);
    }

    private static Dictionary<string, object> GetOrLoadCacheLocked()
    {
        if (cache == null)
            cache = LoadFromDisk();
        return cache;
    }

    private static Dictionary<string, object> LoadFromDisk()
    {
        try
        {
            string? directory = Path.GetDirectoryName(JsonPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(JsonPath))
                return new Dictionary<string, object>();

            JObject json = JObject.Parse(File.ReadAllText(JsonPath));
            var loaded = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in json.Properties())
            {
                loaded[property.Name] = property.Value.Type switch
                {
                    JTokenType.Boolean => property.Value.Value<bool>(),
                    JTokenType.Integer => property.Value.Value<int>(),
                    JTokenType.Float => property.Value.Value<double>(),
                    _ => property.Value.ToString(),
                };
            }

            return loaded;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private static void PersistLocked()
    {
        string? directory = Path.GetDirectoryName(JsonPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(JsonPath, JsonConvert.SerializeObject(cache));
    }
}
