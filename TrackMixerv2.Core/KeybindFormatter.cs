namespace TrackMixerv2;

public static class KeybindFormatter
{
  private static readonly Dictionary<int, string> KeyNames = new()
  {
      [0x20] = "Space",
      [0x1B] = "Esc",
      [0x25] = "←",
      [0x27] = "→",
      [0x21] = "PgUp",
      [0x22] = "PgDn",
      [0xA0] = "Shift",
      [0xA1] = "Shift",
      [0xA2] = "Ctrl",
      [0xA3] = "Ctrl",
      [0xA4] = "Alt",
      [0xA5] = "Alt",
  };

  public static string Format(KeybindChord chord)
  {
    var parts = new List<string>();
    int mods = chord.Modifiers;

    if ((mods & 1) != 0)
      parts.Add("Shift");
    if ((mods & 2) != 0)
      parts.Add("Ctrl");
    if ((mods & 4) != 0)
      parts.Add("Alt");
    if ((mods & 8) != 0)
      parts.Add("Win");

    parts.Add(FormatKey(chord.Key));
    return string.Join("+", parts);
  }

  public static string FormatKey(int virtualKey)
  {
    if (KeyNames.TryGetValue(virtualKey, out string? name))
      return name;

    if (virtualKey is >= 0x41 and <= 0x5A)
      return ((char)virtualKey).ToString();

    if (virtualKey is >= 0x30 and <= 0x39)
      return ((char)virtualKey).ToString();

    if (virtualKey is >= 0x70 and <= 0x7B)
      return "F" + (virtualKey - 0x6F);

    return "Key " + virtualKey;
  }
}
