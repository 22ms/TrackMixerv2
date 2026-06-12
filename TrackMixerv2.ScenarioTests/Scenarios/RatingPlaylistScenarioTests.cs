using TrackMixerv2;
using TrackMixerv2.ScenarioTests.Support;
using static TrackMixerv2.PlaylistHelper;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class RatingPlaylistScenarioTests : IDisposable
{
    private readonly ClipLibrary _library = new(nameof(RatingPlaylistScenarioTests));
    private readonly PlaylistConfig _config = new(PlaylistMode.Rating, TimeSpan.FromDays(30), subfolderOnly: false);

    public RatingPlaylistScenarioTests()
    {
        AppState.Reset();
        _library.InstallAsRootFolder();
        _config.TimeSpan = TimeSpan.FromDays(30);
    }

    [Fact]
    public async Task Rating_playlist_advances_by_score_not_capture_time()
    {
        var now = DateTime.Now.AddHours(-1);
        string low = _library.AddClip("low.mp4", now);
        string mid = _library.AddClip("mid.mp4", now.AddMinutes(1));
        string high = _library.AddClip("high.mp4", now.AddMinutes(2));

        AppState.TRACK_METADATA[low] = new TrackMetadata(2, new List<double> { 100 });
        AppState.TRACK_METADATA[mid] = new TrackMetadata(5, new List<double> { 100 });
        AppState.TRACK_METADATA[high] = new TrackMetadata(9, new List<double> { 100 });

        Assert.Equal(mid, await GetTrack(_config, high, Direction.Next));
        Assert.Equal(low, await GetTrack(_config, mid, Direction.Next));
        Assert.Equal(mid, await GetTrack(_config, low, Direction.Previous));
    }

    [Fact]
    public void Unrated_clip_is_redirected_to_top_rated_clip_in_window()
    {
        var now = DateTime.Now.AddHours(-2);
        string unrated = _library.AddClip("new-clip.mp4", now);
        string ratedLow = _library.AddClip("rated-low.mp4", now.AddMinutes(-5));
        string ratedHigh = _library.AddClip("rated-high.mp4", now.AddMinutes(-10));

        AppState.TRACK_METADATA[ratedLow] = new TrackMetadata(3, new List<double> { 80 });
        AppState.TRACK_METADATA[ratedHigh] = new TrackMetadata(8, new List<double> { 60 });

        Assert.Equal(ratedHigh, IsInRatings(_config, unrated));
    }

    [Fact]
    public void Rated_clip_stays_put_when_already_in_rating_playlist()
    {
        var now = DateTime.Now.AddHours(-1);
        string clip = _library.AddClip("rated.mp4", now);
        string other = _library.AddClip("other.mp4", now.AddMinutes(-3));

        AppState.TRACK_METADATA[clip] = new TrackMetadata(7, new List<double> { 100 });
        AppState.TRACK_METADATA[other] = new TrackMetadata(9, new List<double> { 100 });

        Assert.Null(IsInRatings(_config, clip));
    }

    public void Dispose()
    {
        _library.Dispose();
        AppState.Reset();
    }
}
