using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TrackMixerv2;

public enum KeybindAction
{
    PlayPause,
    Rewind,
    FastForward,
    SpeedBoost,
    SpeedSlow,
    NewTab,
    CloseTab,
    NextTab,
    PreviousTab,
    ExitFullscreen,
}

public sealed class KeybindChord
{
    public int Key { get; set; }
    public int Modifiers { get; set; }

    public KeybindChord()
    {
    }

    public KeybindChord(int key, int modifiers = 0)
    {
        Key = key;
        Modifiers = modifiers;
    }
}

public static class KeybindStore
{
  public static class Keys
  {
    public const string KeybindsJson = "KeybindsJson";
  }

  private static readonly object Lock = new();
  private static Dictionary<KeybindAction, KeybindChord>? cache;

  public static IReadOnlyDictionary<KeybindAction, KeybindChord> Defaults { get; } =
      new Dictionary<KeybindAction, KeybindChord>
      {
          [KeybindAction.PlayPause] = new(0x20), // Space
          [KeybindAction.Rewind] = new(0x25), // Left
          [KeybindAction.FastForward] = new(0x27), // Right
          [KeybindAction.SpeedBoost] = new(0xA0), // LeftShift
          [KeybindAction.SpeedSlow] = new(0xA2), // LeftControl
          [KeybindAction.NewTab] = new(0x54, 2), // T + Control
          [KeybindAction.CloseTab] = new(0x57, 2), // W + Control
          [KeybindAction.NextTab] = new(0x21, 2), // PageUp + Control
          [KeybindAction.PreviousTab] = new(0x22, 2), // PageDown + Control
          [KeybindAction.ExitFullscreen] = new(0x1B), // Escape
      };

  public static IReadOnlyList<(KeybindAction Action, string Label)> DisplayOrder { get; } =
  [
      (KeybindAction.PlayPause, "Play / pause"),
      (KeybindAction.Rewind, "Rewind"),
      (KeybindAction.FastForward, "Forward"),
      (KeybindAction.SpeedBoost, "Speed boost (hold)"),
      (KeybindAction.SpeedSlow, "Slow motion (hold)"),
      (KeybindAction.NewTab, "New tab"),
      (KeybindAction.CloseTab, "Close tab"),
      (KeybindAction.NextTab, "Next tab"),
      (KeybindAction.PreviousTab, "Previous tab"),
      (KeybindAction.ExitFullscreen, "Exit fullscreen"),
  ];

  public static KeybindChord Get(KeybindAction action)
  {
    EnsureLoaded();
    lock (Lock)
      return cache![action];
  }

  public static string GetActionLabel(KeybindAction action)
  {
    foreach (var (itemAction, label) in DisplayOrder)
    {
      if (itemAction != action)
        continue;

      return action switch
      {
        KeybindAction.Rewind => $"Rewind {LocalSettingsStore.GetSkipSeconds()}s",
        KeybindAction.FastForward => $"Forward {LocalSettingsStore.GetSkipSeconds()}s",
        _ => label,
      };
    }

    return action.ToString();
  }

  public static void Set(KeybindAction action, KeybindChord chord)
  {
    if (!TryValidateChord(action, chord, out _))
      return;

    EnsureLoaded();
    lock (Lock)
    {
      cache![action] = chord;
      PersistLocked();
    }
  }

  public static bool TryValidateChord(KeybindAction action, KeybindChord chord, out string? error) =>
      KeybindFocusRules.TryValidateChord(action, chord, out error);

  public static bool TrySet(KeybindAction action, KeybindChord chord, out string? error)
  {
    if (!TryValidateChord(action, chord, out error))
      return false;

    EnsureLoaded();
    lock (Lock)
    {
      cache![action] = chord;
      PersistLocked();
    }

    error = null;
    return true;
  }

  public static void ResetToDefaults()
  {
    lock (Lock)
    {
      cache = CloneDefaults();
      PersistLocked();
    }
  }

  public static void ResetCache()
  {
    lock (Lock)
      cache = null;
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

  private static Dictionary<KeybindAction, KeybindChord> LoadFromDisk()
  {
    var loaded = CloneDefaults();
    if (!LocalSettingsStore.ContainsKey(Keys.KeybindsJson))
      return loaded;

    try
    {
      string? json = LocalSettingsStore.GetString(Keys.KeybindsJson);
      if (string.IsNullOrWhiteSpace(json))
        return loaded;

      var parsed = JsonConvert.DeserializeObject<Dictionary<string, KeybindChord>>(json);
      if (parsed == null)
        return loaded;

      foreach (var pair in parsed)
      {
        if (Enum.TryParse<KeybindAction>(pair.Key, out var action) && pair.Value != null)
          loaded[action] = pair.Value;
      }
    }
    catch
    {
    }

    return loaded;
  }

  private static void PersistLocked()
  {
    var serializable = cache!.ToDictionary(
        pair => pair.Key.ToString(),
        pair => pair.Value);
    LocalSettingsStore.SetString(Keys.KeybindsJson, JsonConvert.SerializeObject(serializable));
  }

  private static Dictionary<KeybindAction, KeybindChord> CloneDefaults() =>
      Defaults.ToDictionary(pair => pair.Key, pair => new KeybindChord(pair.Value.Key, pair.Value.Modifiers));
}
