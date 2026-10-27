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

        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, clipA, 4, new List<double> { 90 });
        var saveA = TrackMetadataStore.PersistAsync(AppState.TRACK_METADATA, _metadataPath);
        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, clipB, 8, new List<double> { 10, 20 });
        var saveB = TrackMetadataStore.PersistAsync(AppState.TRACK_METADATA, _metadataPath);

        await Task.WhenAll(saveA, saveB);

        var reloaded = TrackMetadataStore.Load(_metadataPath);
        Assert.Equal(4, reloaded[clipA].Rating);
        Assert.Equal(8, reloaded[clipB].Rating);
        Assert.Equal(new List<double> { 90 }, reloaded[clipA].Sliders);
        Assert.Equal(new List<double> { 10, 20 }, reloaded[clipB].Sliders);
    }

    [Fact]
    public void Corrupt_metadata_is_moved_aside_instead_of_crashing_or_being_overwritten()
    {
        const string truncated = "{\"C:\\\\clip.mp4\":{\"Rating\":4,\"Sliders\":[100,";
        File.WriteAllText(_metadataPath, truncated);

        Dictionary<string, TrackMetadata> loaded = TrackMetadataStore.Load(_metadataPath);

        Assert.Empty(loaded);
        Assert.False(File.Exists(_metadataPath));
        string quarantined = Assert.Single(Directory.GetFiles(_library.RootPath, "track_metadata.json.corrupt-*"));
        Assert.Equal(truncated, File.ReadAllText(quarantined));
    }

    [Fact]
    public void Empty_metadata_file_from_older_versions_loads_as_empty()
    {
        File.WriteAllText(_metadataPath, "");

        Assert.Empty(TrackMetadataStore.Load(_metadataPath));
        Assert.True(File.Exists(_metadataPath));
    }

    [Fact]
    public async Task Save_replaces_the_file_in_one_step_and_leaves_no_temp_file()
    {
        string clip = _library.AddClip("clip.mp4", DateTime.Now);
        File.WriteAllText(_metadataPath, "{}");

        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, clip, 3, new List<double> { 50 });
        await TrackMetadataStore.PersistAsync(AppState.TRACK_METADATA, _metadataPath);

        Assert.False(File.Exists(_metadataPath + ".tmp"));
        Assert.Equal(3, TrackMetadataStore.Load(_metadataPath)[clip].Rating);
    }

    [Fact]
    public void UpdateEntry_stores_a_copy_of_the_slider_list()
    {
        string clip = _library.AddClip("clip.mp4", DateTime.Now);
        var sliders = new List<double> { 100, 100 };

        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, clip, 1, sliders);
        sliders[1] = 0;

        Assert.Equal(new List<double> { 100, 100 }, AppState.TRACK_METADATA[clip].Sliders);
    }

    [Fact]
    public async Task Paths_match_regardless_of_case()
    {
        string clip = _library.AddClip("Clip.mp4", DateTime.Now);
        TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, clip, 5, new List<double> { 100 });
        await TrackMetadataStore.PersistAsync(AppState.TRACK_METADATA, _metadataPath);

        Assert.True(TrackMetadataStore.Load(_metadataPath).ContainsKey(clip.ToUpperInvariant()));
    }

    public void Dispose()
    {
        _library.Dispose();
        Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, null);
        AppState.Reset();
    }
}
