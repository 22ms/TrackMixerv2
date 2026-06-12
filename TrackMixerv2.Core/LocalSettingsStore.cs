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
    }

    private static readonly object Lock = new();
    private static Dictionary<string, object>? cache;

    public const string JsonPathEnvVar = "TRACKMIXER_LOCAL_SETTINGS_PATH";

    public static string JsonPath =>
        Environment.GetEnvironmentVariable(JsonPathEnvVar)
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TrackMixerv2",
            "local_settings.json");

    public static void ResetCache()
    {
        lock (Lock)
            cache = null;
    }

    public static bool ContainsKey(string key)
    {
        EnsureLoaded();
        lock (Lock)
            return cache!.ContainsKey(key);
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

    public static void SetBool(string key, bool value) => Set(key, value);

    public static void SetString(string key, string value) => Set(key, value);

    public static void Set(string key, object value)
    {
        EnsureLoaded();
        lock (Lock)
        {
            cache![key] = value;
            PersistLocked();
        }
    }

    private static bool TryGetValue(string key, out object? value)
    {
        EnsureLoaded();
        lock (Lock)
            return cache!.TryGetValue(key, out value);
    }

    private static void EnsureLoaded()
    {
        if (cache != null)
            return;

        lock (Lock)
        {
            if (cache != null)
                return;

            cache = LoadFromDisk();
        }
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
                loaded[property.Name] = property.Value.Type == JTokenType.Boolean
                    ? property.Value.Value<bool>()
                    : property.Value.ToString();

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
