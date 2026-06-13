using TrackMixerv2;
using TrackMixerv2.ScenarioTests.Support;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class PlaylistNavigationScenarioTests : IDisposable
{
    private readonly ClipLibrary _library = new(nameof(PlaylistNavigationScenarioTests));

    public PlaylistNavigationScenarioTests()
    {
        AppState.Reset();
        _library.InstallAsRootFolder();
    }

    [Fact]
    public void FindExistingPathForward_skips_missing_files()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string first = _library.AddClip("first.mp4", baseTime);
        string second = _library.AddClip("second.mp4", baseTime.AddMinutes(1));
        string third = _library.AddClip("third.mp4", baseTime.AddMinutes(2));
        File.Delete(second);

        PlaylistIndex index = PlaylistIndexCache.GetChrono(_library.RootPath);
        Assert.True(index.TryGetIndex(first, out int firstIndex));

        Assert.Equal(third, PlaylistNavigation.FindExistingPathForward(index, firstIndex + 1));
    }

    [Fact]
    public async Task GetTrack_rebuilds_chrono_when_at_last_cached_clip_and_new_file_appears()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string first = _library.AddClip("first.mp4", baseTime);
        string second = _library.AddClip("second.mp4", baseTime.AddMinutes(1));

        var config = new PlaylistHelper.PlaylistConfig(PlaylistHelper.PlaylistMode.Chrono, TimeSpan.FromDays(30), subfolderOnly: false);
        string? beforeNewClip = await PlaylistHelper.GetTrack(config, second, PlaylistHelper.Direction.Next);
        Assert.Null(beforeNewClip);

        string third = _library.AddClip("third.mp4", baseTime.AddMinutes(2));
        string? afterNewClip = await PlaylistHelper.GetTrack(config, second, PlaylistHelper.Direction.Next);

        Assert.Equal(third, afterNewClip);
        Assert.NotEqual(first, afterNewClip);
    }

    [Fact]
    public async Task GetTrack_falls_back_to_previous_when_deleting_latest_chrono_clip()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string first = _library.AddClip("first.mp4", baseTime);
        string latest = _library.AddClip("latest.mp4", baseTime.AddMinutes(5));

        var config = new PlaylistHelper.PlaylistConfig(PlaylistHelper.PlaylistMode.Chrono, TimeSpan.FromDays(30), subfolderOnly: false);

        Assert.Null(await PlaylistHelper.GetTrack(config, latest, PlaylistHelper.Direction.Next));
        Assert.Equal(first, await PlaylistHelper.GetTrack(config, latest, PlaylistHelper.Direction.Previous));
    }

    public void Dispose()
    {
        _library.Dispose();
        AppState.Reset();
    }
}
