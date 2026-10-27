using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class KeybindScenarioTests : IDisposable
{
    private readonly string _settingsPath;
    private readonly string? _previousSettingsPathEnv;

    public KeybindScenarioTests()
    {
        _previousSettingsPathEnv = Environment.GetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar);
        _settingsPath = Path.Combine(
            Path.GetTempPath(),
            "TrackMixerScenarioTests",
            Guid.NewGuid().ToString("N"),
            "local_settings.json");
        Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, _settingsPath);
        LocalSettingsStore.ResetCache();
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
        Assert.True(KeybindStore.TrySet(KeybindAction.Rewind, new KeybindChord(0x42, 2), out _));
        KeybindStore.ResetCache();

        Assert.Equal(0x42, KeybindStore.Get(KeybindAction.Rewind).Key);
        Assert.Equal(2, KeybindStore.Get(KeybindAction.Rewind).Modifiers);
        Assert.Equal("Ctrl+B", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.Rewind)));
    }

    [Fact]
    public void Set_rejects_tab_and_leaves_previous_binding()
    {
        KeybindStore.ResetCache();
        KeybindChord original = KeybindStore.Get(KeybindAction.PlayPause);

        Assert.False(KeybindStore.TrySet(KeybindAction.PlayPause, new KeybindChord(0x09), out string? error));

        Assert.NotNull(error);
        Assert.Equal(original.Key, KeybindStore.Get(KeybindAction.PlayPause).Key);
        Assert.Equal(original.Modifiers, KeybindStore.Get(KeybindAction.PlayPause).Modifiers);
    }

    [Fact]
    public void TrySet_rejects_a_chord_another_action_already_uses()
    {
        KeybindStore.ResetCache();

        Assert.False(KeybindStore.TrySet(KeybindAction.Rewind, new KeybindChord(0x54, 2), out string? error));

        Assert.Contains("New tab", error);
        Assert.Equal(0x25, KeybindStore.Get(KeybindAction.Rewind).Key);
        Assert.Equal(0x54, KeybindStore.Get(KeybindAction.NewTab).Key);
    }

    [Fact]
    public void TrySet_treats_left_and_right_modifier_holds_as_the_same_chord()
    {
        KeybindStore.ResetCache();

        Assert.False(KeybindStore.TrySet(KeybindAction.SpeedBoost, new KeybindChord(0xA3), out _));
    }

    [Fact]
    public void TrySet_allows_rebinding_an_action_to_its_own_chord()
    {
        KeybindStore.ResetCache();

        Assert.True(KeybindStore.TrySet(KeybindAction.PlayPause, new KeybindChord(0x20), out _));
    }

    [Fact]
    public void Default_playback_shortcuts_use_natural_unmodified_keys()
    {
        KeybindStore.ResetCache();

        Assert.Equal(0x20, KeybindStore.Get(KeybindAction.PlayPause).Key);
        Assert.Equal(0, KeybindStore.Get(KeybindAction.PlayPause).Modifiers);
        Assert.Equal(0x25, KeybindStore.Get(KeybindAction.Rewind).Key);
        Assert.Equal(0x27, KeybindStore.Get(KeybindAction.FastForward).Key);
    }

    [Fact]
    public void Fine_skips_default_to_shift_arrows_and_one_second()
    {
        KeybindStore.ResetCache();

        Assert.Equal("Shift+←", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.FineRewind)));
        Assert.Equal("Shift+→", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.FineForward)));
        Assert.Equal("Fine rewind 1s", KeybindStore.GetActionLabel(KeybindAction.FineRewind));

        LocalSettingsStore.SetFineSkipSeconds(3);
        Assert.Equal("Fine forward 3s", KeybindStore.GetActionLabel(KeybindAction.FineForward));
    }

    [Fact]
    public void A_new_default_never_takes_a_chord_the_user_already_saved()
    {
        LocalSettingsStore.SetString(
            KeybindStore.Keys.KeybindsJson,
            """{"NewTab":{"Key":37,"Modifiers":1}}""");
        KeybindStore.ResetCache();

        Assert.Equal("Shift+←", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.NewTab)));
        Assert.Equal("Not set", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.FineRewind)));
        Assert.Equal("Shift+→", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.FineForward)));
    }

    [Fact]
    public void Toggle_fullscreen_defaults_to_f11_and_rejects_escape()
    {
        KeybindStore.ResetCache();

        Assert.Equal(0x7A, KeybindStore.Get(KeybindAction.ToggleFullscreen).Key);
        Assert.Equal(0, KeybindStore.Get(KeybindAction.ToggleFullscreen).Modifiers);
        Assert.Equal("F11", KeybindFormatter.Format(KeybindStore.Get(KeybindAction.ToggleFullscreen)));
        Assert.False(KeybindStore.TrySet(KeybindAction.ToggleFullscreen, new KeybindChord(0x1B), out string? error));
        Assert.Contains("Escape", error, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, _previousSettingsPathEnv);
        LocalSettingsStore.ResetCache();
        KeybindStore.ResetCache();

        string? directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
