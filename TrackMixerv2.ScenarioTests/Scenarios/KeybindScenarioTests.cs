using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class KeybindScenarioTests : IDisposable
{
  private readonly string? _previousKeybindsJson;

  public KeybindScenarioTests()
  {
    _previousKeybindsJson = LocalSettingsStore.GetString(KeybindStore.Keys.KeybindsJson);
    KeybindStore.ResetCache();
  }

  [Fact]
  public void Keybind_defaults_format_to_readable_shortcuts()
  {
    Assert.Equal("Space", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.PlayPause)));
    Assert.Equal("Ctrl+W", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.CloseTab)));
  }

  [Fact]
  public void Custom_keybind_round_trips_through_local_settings()
  {
    KeybindStore.Set(KeybindAction.Rewind, new KeybindChord(0x42, 2));
    KeybindStore.ResetCache();

    Assert.Equal(0x42, KeybindStore.Get(KeybindAction.Rewind).Key);
    Assert.Equal(2, KeybindStore.Get(KeybindAction.Rewind).Modifiers);
    Assert.Equal("Ctrl+B", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.Rewind)));
  }

  public void Dispose()
  {
    if (_previousKeybindsJson == null)
      LocalSettingsStore.SetString(KeybindStore.Keys.KeybindsJson, string.Empty);
    else
      LocalSettingsStore.SetString(KeybindStore.Keys.KeybindsJson, _previousKeybindsJson);

    KeybindStore.ResetCache();
    LocalSettingsStore.ResetCache();
  }
}
