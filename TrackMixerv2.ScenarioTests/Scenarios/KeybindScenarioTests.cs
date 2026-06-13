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
        KeybindStore.Set(KeybindAction.Rewind, new KeybindChord(0x42, 2));
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

        KeybindStore.Set(KeybindAction.PlayPause, new KeybindChord(0x09));

        Assert.Equal(original.Key, KeybindStore.Get(KeybindAction.PlayPause).Key);
        Assert.Equal(original.Modifiers, KeybindStore.Get(KeybindAction.PlayPause).Modifiers);
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
