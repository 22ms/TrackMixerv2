using TrackMixerv2;
using TrackMixerv2.ScenarioTests.Support;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class RatingSessionScenarioTests : IDisposable
{
    private readonly ClipLibrary _library = new(nameof(RatingSessionScenarioTests));
    private readonly string _metadataPath;

    public RatingSessionScenarioTests()
    {
        AppState.Reset();
        _metadataPath = Path.Combine(_library.RootPath, "track_metadata.json");
        Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, _metadataPath);
    }

    [Fact]
    public async Task Multi_clip_review_session_persists_each_rating_independently()
    {
        string clipA = _library.AddClip("session-a.mp4", DateTime.Now.AddHours(-2));
        string clipB = _library.AddClip("session-b.mp4", DateTime.Now.AddHours(-1));

        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, clipA, rating: 25, new List<double> { 100, 50 });
        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, clipB, rating: 80, new List<double> { 0, 100 });
        await TrackMetadataStore.PersistAsync(AppState.TRACK_METADATA, _metadataPath);

        AppState.Reset();
        Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, _metadataPath);
        AppState.TRACK_METADATA = TrackMetadataStore.Load(_metadataPath);

        Assert.Equal(25, AppState.TRACK_METADATA[clipA].Rating);
        Assert.Equal(80, AppState.TRACK_METADATA[clipB].Rating);
        Assert.Equal(new List<double> { 100, 50 }, AppState.TRACK_METADATA[clipA].Sliders);
        Assert.Equal(new List<double> { 0, 100 }, AppState.TRACK_METADATA[clipB].Sliders);
    }

    public void Dispose()
    {
        _library.Dispose();
        Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, null);
        AppState.Reset();
    }
}
