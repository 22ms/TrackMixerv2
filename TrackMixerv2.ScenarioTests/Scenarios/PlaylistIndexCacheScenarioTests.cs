using TrackMixerv2;
using TrackMixerv2.ScenarioTests.Support;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class PlaylistIndexCacheScenarioTests : IDisposable
{
    private readonly ClipLibrary _library = new(nameof(PlaylistIndexCacheScenarioTests));

    public PlaylistIndexCacheScenarioTests()
    {
        AppState.Reset();
        _library.InstallAsRootFolder();
    }

    [Fact]
    public void Chrono_index_is_reused_until_a_new_clip_is_opened()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        _library.AddClip("clip-a.mp4", baseTime);
        _library.AddClip("clip-b.mp4", baseTime.AddMinutes(5));

        PlaylistIndex first = PlaylistIndexCache.GetChrono(_library.RootPath);
        PlaylistIndex second = PlaylistIndexCache.GetChrono(_library.RootPath);

        Assert.Same(first, second);
        Assert.Equal(2, first.OrderedPaths.Count);

        string clipC = _library.AddClip("clip-c.mp4", baseTime.AddMinutes(10));
        PlaylistIndex rebuilt = PlaylistIndexCache.GetChronoOrRebuild(_library.RootPath, clipC);

        Assert.Equal(3, rebuilt.OrderedPaths.Count);
        Assert.True(rebuilt.TryGetIndex(clipC, out _));
    }

    [Fact]
    public void Rating_index_is_invalidated_when_metadata_changes()
    {
        var now = DateTime.Now.AddHours(-1);
        string low = _library.AddClip("low.mp4", now);
        string high = _library.AddClip("high.mp4", now.AddMinutes(1));
        DateTime afterThis = DateTime.Now.Subtract(TimeSpan.FromDays(30));

        AppState.TRACK_METADATA[low] = new TrackMetadata(2, new List<double> { 100 });
        AppState.TRACK_METADATA[high] = new TrackMetadata(9, new List<double> { 100 });

        PlaylistIndex first = PlaylistIndexCache.GetRating(_library.RootPath, afterThis);
        Assert.Equal(high, first.OrderedPaths[0]);

        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, low, rating: 10, new List<double> { 100 });
        PlaylistIndex second = PlaylistIndexCache.GetRating(_library.RootPath, afterThis);

        Assert.Equal(low, second.OrderedPaths[0]);
    }

    [Fact]
    public async Task PrewarmPlaylistIndex_populates_chrono_cache_in_background()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        _library.AddClip("clip-a.mp4", baseTime);
        string clipB = _library.AddClip("clip-b.mp4", baseTime.AddMinutes(5));

        var config = new PlaylistHelper.PlaylistConfig(PlaylistHelper.PlaylistMode.Chrono, TimeSpan.FromDays(30), subfolderOnly: false);
        PlaylistHelper.PrewarmPlaylistIndex(config, clipB);

        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (PlaylistIndexCache.GetChrono(_library.RootPath).OrderedPaths.Count == 2)
                return;

            await Task.Delay(50);
        }

        Assert.Fail("Prewarm did not populate chrono index in time.");
    }

    public void Dispose()
    {
        _library.Dispose();
        AppState.Reset();
    }
}
