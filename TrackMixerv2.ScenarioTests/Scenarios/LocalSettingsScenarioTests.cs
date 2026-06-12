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
