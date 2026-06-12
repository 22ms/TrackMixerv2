using TrackMixerv2;
using TrackMixerv2.ScenarioTests.Support;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class MetadataPersistenceScenarioTests : IDisposable
{
    private readonly ClipLibrary _library = new(nameof(MetadataPersistenceScenarioTests));
    private readonly string _metadataPath;

    public MetadataPersistenceScenarioTests()
    {
        AppState.Reset();
        _metadataPath = Path.Combine(_library.RootPath, "track_metadata.json");
        Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, _metadataPath);
    }

    [Fact]
    public async Task Ratings_and_slider_values_survive_save_reload_cycle()
    {
        string clip = _library.AddClip("session\\clip.mp4", DateTime.Now.AddHours(-1));
        var sliders = new List<double> { 100, 40, 0 };

        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, clip, rating: 7, sliders);
        await TrackMetadataStore.PersistAsync(AppState.TRACK_METADATA, _metadataPath);

        AppState.Reset();
        Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, _metadataPath);
        AppState.TRACK_METADATA = TrackMetadataStore.Load(_metadataPath);

        Assert.True(AppState.TRACK_METADATA.ContainsKey(clip));
        Assert.Equal(7, AppState.TRACK_METADATA[clip].Rating);
        Assert.Equal(sliders, AppState.TRACK_METADATA[clip].Sliders);
    }

    [Fact]
    public async Task Concurrent_tab_edits_do_not_lose_the_last_saved_clip()
    {
        string clipA = _library.AddClip("a.mp4", DateTime.Now.AddHours(-2));
        string clipB = _library.AddClip("b.mp4", DateTime.Now.AddHours(-1));

        var saveA = TrackMetadataStore.UpdateAndPersistAsync(AppState.TRACK_METADATA, _metadataPath, clipA, 4, new List<double> { 90 });
        var saveB = TrackMetadataStore.UpdateAndPersistAsync(AppState.TRACK_METADATA, _metadataPath, clipB, 8, new List<double> { 10, 20 });

        await Task.WhenAll(saveA, saveB);

        var reloaded = TrackMetadataStore.Load(_metadataPath);
        Assert.Equal(4, reloaded[clipA].Rating);
        Assert.Equal(8, reloaded[clipB].Rating);
        Assert.Equal(new List<double> { 90 }, reloaded[clipA].Sliders);
        Assert.Equal(new List<double> { 10, 20 }, reloaded[clipB].Sliders);
    }

    public void Dispose()
    {
        _library.Dispose();
        Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, null);
        AppState.Reset();
    }
}
