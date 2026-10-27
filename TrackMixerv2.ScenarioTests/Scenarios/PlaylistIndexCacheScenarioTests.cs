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
        TimeSpan window = TimeSpan.FromDays(30);

        AppState.TRACK_METADATA[low] = new TrackMetadata(2, new List<double> { 100 });
        AppState.TRACK_METADATA[high] = new TrackMetadata(9, new List<double> { 100 });

        PlaylistIndex first = PlaylistIndexCache.GetRating(_library.RootPath, window);
        Assert.Equal(high, first.OrderedPaths[0]);

        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, low, rating: 10, new List<double> { 100 });
        PlaylistIndex second = PlaylistIndexCache.GetRating(_library.RootPath, window);

        Assert.Equal(low, second.OrderedPaths[0]);
    }

    [Fact]
    public void Rating_index_is_reused_while_nothing_changes()
    {
        string clip = _library.AddClip("rated.mp4", DateTime.Now.AddHours(-1));
        AppState.TRACK_METADATA[clip] = new TrackMetadata(5, new List<double> { 100 });

        PlaylistIndex first = PlaylistIndexCache.GetRating(_library.RootPath, TimeSpan.FromDays(30));
        PlaylistIndex second = PlaylistIndexCache.GetRating(_library.RootPath, TimeSpan.FromDays(30));

        Assert.Same(first, second);
    }

    [Fact]
    public void Missing_root_folder_gives_an_empty_index_instead_of_throwing()
    {
        string missing = Path.Combine(_library.RootPath, "unplugged-drive");

        Assert.Empty(PlaylistIndexCache.GetChrono(missing).OrderedPaths);
    }

    public void Dispose()
    {
        _library.Dispose();
        AppState.Reset();
    }
}
