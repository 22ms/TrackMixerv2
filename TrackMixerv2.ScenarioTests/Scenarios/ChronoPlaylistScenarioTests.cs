using TrackMixerv2;
using TrackMixerv2.ScenarioTests.Support;
using static TrackMixerv2.PlaylistHelper;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class ChronoPlaylistScenarioTests : IDisposable
{
    private readonly ClipLibrary _library = new(nameof(ChronoPlaylistScenarioTests));
    private readonly PlaylistConfig _config = new(PlaylistMode.Chrono, TimeSpan.FromDays(365), subfolderOnly: false);

    public ChronoPlaylistScenarioTests()
    {
        AppState.Reset();
        _library.InstallAsRootFolder();
    }

    [Fact]
    public async Task User_steps_through_clips_in_capture_order()
    {
        var baseTime = DateTime.Now.AddDays(-5);
        string clip1 = _library.AddClip("session-a\\clip1.mp4", baseTime);
        string clip2 = _library.AddClip("session-a\\clip2.mp4", baseTime.AddMinutes(10));
        string clip3 = _library.AddClip("session-b\\clip3.mp4", baseTime.AddMinutes(20));

        Assert.Null(await GetTrack(_config, clip3, Direction.Next));

        string? step1 = await GetTrack(_config, clip1, Direction.Next);
        string? step2 = await GetTrack(_config, step1!, Direction.Next);

        Assert.Equal(clip2, step1);
        Assert.Equal(clip3, step2);
    }

    [Fact]
    public async Task User_steps_backwards_through_clips_in_capture_order()
    {
        var baseTime = DateTime.Now.AddDays(-5);
        string clip1 = _library.AddClip("session-a\\clip1.mp4", baseTime);
        string clip2 = _library.AddClip("session-a\\clip2.mp4", baseTime.AddMinutes(10));
        string clip3 = _library.AddClip("session-b\\clip3.mp4", baseTime.AddMinutes(20));

        Assert.Equal(clip2, await GetTrack(_config, clip3, Direction.Previous));
        Assert.Equal(clip1, await GetTrack(_config, clip2, Direction.Previous));
        Assert.Null(await GetTrack(_config, clip1, Direction.Previous));
    }

    [Fact]
    public async Task Subfolder_only_mode_stays_inside_current_folder()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string clipA1 = _library.AddClip("session-a\\a1.mp4", baseTime);
        string clipA2 = _library.AddClip("session-a\\a2.mp4", baseTime.AddMinutes(5));
        _library.AddClip("session-b\\b1.mp4", baseTime.AddMinutes(10));

        _config.SubfolderOnly = true;

        Assert.Equal(clipA2, await GetTrack(_config, clipA1, Direction.Next));
        Assert.Null(await GetTrack(_config, clipA2, Direction.Next));
    }

    public void Dispose()
    {
        _library.Dispose();
        AppState.Reset();
    }
}
