using Newtonsoft.Json;
using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class LocalSettingsScenarioTests : IDisposable
{
    private readonly string _settingsPath;

    public LocalSettingsScenarioTests()
    {
        _settingsPath = Path.Combine(
            Path.GetTempPath(),
            "TrackMixerScenarioTests",
            Guid.NewGuid().ToString("N"),
            "local_settings.json");
        Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, _settingsPath);
        LocalSettingsStore.ResetCache();
    }

    [Fact]
    public void Preference_flags_round_trip_through_disk()
    {
        LocalSettingsStore.SetBool(LocalSettingsStore.Keys.DoubleClickOnNewTab, true);
        LocalSettingsStore.SetBool(LocalSettingsStore.Keys.DragAndDropOnNewTab, false);
        LocalSettingsStore.ResetCache();

        Assert.True(LocalSettingsStore.GetBool(LocalSettingsStore.Keys.DoubleClickOnNewTab));
        Assert.False(LocalSettingsStore.GetBool(LocalSettingsStore.Keys.DragAndDropOnNewTab));
    }

    [Fact]
    public void Skip_seconds_round_trip_through_disk()
    {
        LocalSettingsStore.SetSkipSeconds(15);
        LocalSettingsStore.ResetCache();

        Assert.Equal(15, LocalSettingsStore.GetSkipSeconds());
        Assert.Equal("Rewind 15s", KeybindStore.GetActionLabel(KeybindAction.Rewind));
        Assert.Equal("Forward 15s", KeybindStore.GetActionLabel(KeybindAction.FastForward));
    }

    [Fact]
    public void Slider_wheel_speed_round_trip_through_disk()
    {
        LocalSettingsStore.SetSliderWheelSpeed(4);
        LocalSettingsStore.ResetCache();

        Assert.Equal(4, LocalSettingsStore.GetSliderWheelSpeed());
        Assert.Equal(20, SliderWheelRules.GetWheelStep(0, 5, LocalSettingsStore.GetSliderWheelSpeed()));
    }

    [Fact]
    public void Hold_keybind_speed_rates_round_trip_through_disk()
    {
        LocalSettingsStore.SetSpeedBoostRate(3);
        LocalSettingsStore.SetSpeedSlowRate(0.5);
        LocalSettingsStore.ResetCache();

        Assert.Equal(3, LocalSettingsStore.GetSpeedBoostRate());
        Assert.Equal(0.5, LocalSettingsStore.GetSpeedSlowRate());
        Assert.Equal("Speed boost 3× (hold)", KeybindStore.GetActionLabel(KeybindAction.SpeedBoost));
        Assert.Equal("Slow motion 0.5× (hold)", KeybindStore.GetActionLabel(KeybindAction.SpeedSlow));
    }

    [Fact]
    public void Custom_transport_rates_round_trip_through_disk()
    {
        LocalSettingsStore.SetTransportRates(new[] { 0.5, 1, 2.5, 4 });
        LocalSettingsStore.ResetCache();

        Assert.Equal(new[] { 0.5, 1.0, 2.5, 4.0 }, LocalSettingsStore.GetTransportRates());
        Assert.Equal(new[] { 2.5, 4.0 }, PlaybackRates.BoostRates.ToArray());
    }

    [Fact]
    public void Reset_transport_rates_restores_defaults()
    {
        LocalSettingsStore.SetTransportRates(new[] { 0.5, 1, 4 });
        LocalSettingsStore.ResetTransportRates();
        LocalSettingsStore.ResetCache();

        Assert.Equal(PlaybackRates.Defaults, LocalSettingsStore.GetTransportRates());
    }

    [Theory]
    [InlineData(0.1, 1.25)]
    [InlineData(10, 4)]
    public void Hold_keybind_speed_rates_snap_to_transport_speeds(double boostInput, double expectedBoost)
    {
        LocalSettingsStore.SetSpeedBoostRate(boostInput);
        Assert.Equal(expectedBoost, LocalSettingsStore.GetSpeedBoostRate());
    }

    [Theory]
    [InlineData(0.1, 0.25)]
    [InlineData(1, 0.75)]
    public void Hold_keybind_slow_rates_snap_to_transport_speeds(double slowInput, double expectedSlow)
    {
        LocalSettingsStore.SetSpeedSlowRate(slowInput);
        Assert.Equal(expectedSlow, LocalSettingsStore.GetSpeedSlowRate());
    }

    [Fact]
    public void FilterExistingPaths_removes_missing_files()
    {
        string existing = Path.Combine(Path.GetTempPath(), "TrackMixer-existing-" + Guid.NewGuid().ToString("N") + ".mp4");
        File.WriteAllText(existing, "fixture");

        try
        {
            var filtered = Helper.FilterExistingPaths(
            [
                existing,
                Path.Combine(Path.GetTempPath(), "TrackMixer-missing-" + Guid.NewGuid().ToString("N") + ".mp4"),
            ]);

            Assert.Single(filtered);
            Assert.Equal(existing, filtered[0]);
        }
        finally
        {
            File.Delete(existing);
        }
    }

    [Fact]
    public void Recent_videos_list_matches_save_recent_videos_shape()
    {
        var recentVideos = new List<string>
        {
            @"D:\Clips\round1.mp4",
            @"D:\Clips\round2.mp4",
        };

        LocalSettingsStore.SetString(
            LocalSettingsStore.Keys.RecentVideosJson,
            JsonConvert.SerializeObject(recentVideos));
        LocalSettingsStore.ResetCache();

        string? json = LocalSettingsStore.GetString(LocalSettingsStore.Keys.RecentVideosJson);
        var reloaded = JsonConvert.DeserializeObject<List<string>>(json!)!;

        Assert.Equal(recentVideos, reloaded);
    }

    [Fact]
    public void Root_folder_prompt_flag_persists_like_main_window_property()
    {
        LocalSettingsStore.SetBool(LocalSettingsStore.Keys.SuppressRootFolderPrompt, true);
        LocalSettingsStore.ResetCache();

        AppState.RootFolderPromptSuppressed = LocalSettingsStore.GetBool(LocalSettingsStore.Keys.SuppressRootFolderPrompt);

        Assert.True(AppState.RootFolderPromptSuppressed);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, null);
        LocalSettingsStore.ResetCache();

        string? directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
