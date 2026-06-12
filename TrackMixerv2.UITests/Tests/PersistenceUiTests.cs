using FlaUI.Core.AutomationElements;
using Newtonsoft.Json;
using TrackMixerv2;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestPersistenceCollection.Name)]
[Trait("Category", "UI")]
public sealed class PersistenceUiTests : IDisposable
{
    private readonly string _testHome;
    private readonly string _clipPath;
    private TrackMixerAppSession? _session;

    public PersistenceUiTests()
    {
        _testHome = TrackMixerPaths.CreateIsolatedTestHome();
        _clipPath = TrackMixerPaths.CreateTempClipLibrary();
    }

    [Fact]
    public void Rating_survives_app_relaunch()
    {
        const double targetRating = 75;
        _session = CreateSession(_clipPath);
        _session.Launch();

        var ratingSlider = UiWait.Until(
            () => _session.FindByAutomationId("RatingSlider")?.AsSlider(),
            TimeSpan.FromSeconds(20),
            "rating slider");

        ratingSlider.Value = targetRating;

        UiWait.UntilTrue(
            () => MetadataFileContainsRating(targetRating),
            TimeSpan.FromSeconds(15),
            "metadata file to contain saved rating");

        _session.Relaunch();

        ratingSlider = UiWait.Until(
            () => _session.FindByAutomationId("RatingSlider")?.AsSlider(),
            TimeSpan.FromSeconds(20),
            "rating slider after relaunch");

        UiWait.UntilTrue(
            () => Math.Abs(ratingSlider.Value - targetRating) < 1,
            TimeSpan.FromSeconds(10),
            "restored rating on relaunch");

        Assert.InRange(Math.Abs(ratingSlider.Value - targetRating), 0, 1);
    }

    [Fact]
    public void Recent_videos_restore_on_relaunch_without_launch_file()
    {
        (string clipA, string clipB, string root) = TrackMixerPaths.CreateTwoClipLibrary();
        string testHome = TrackMixerPaths.CreateIsolatedTestHome();

        try
        {
            SeedRecentVideos(testHome, clipA, clipB);

            _session = new TrackMixerAppSession(libraryRoot: root, deleteClipDirectoryOnDispose: false);
            _session.SetTestStorageDirectory(testHome);
            _session.Launch();

            UiWait.UntilTrue(
                () => _session.GetTabCount() >= 2 || _session.CountByAutomationId("RatingSlider") >= 2,
                TimeSpan.FromSeconds(25),
                "restored tabs for recent videos");

            Assert.True(_session.GetTabCount() >= 2 || _session.CountByAutomationId("RatingSlider") >= 2);
        }
        finally
        {
            _session?.Close();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            if (Directory.Exists(testHome))
                Directory.Delete(testHome, recursive: true);
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        if (Directory.Exists(_testHome))
            Directory.Delete(_testHome, recursive: true);
        Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, null);
        Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, null);
        LocalSettingsStore.ResetCache();
    }

    private TrackMixerAppSession CreateSession(string clipPath)
    {
        var session = new TrackMixerAppSession(clipPath, deleteClipDirectoryOnDispose: false);
        session.SetTestStorageDirectory(_testHome);
        return session;
    }

    private static void SeedRecentVideos(string testHome, params string[] clipPaths)
    {
        Directory.CreateDirectory(testHome);
        var settings = new Dictionary<string, object>
        {
            [LocalSettingsStore.Keys.RecentVideosJson] = JsonConvert.SerializeObject(clipPaths),
        };
        File.WriteAllText(
            Path.Combine(testHome, "local_settings.json"),
            JsonConvert.SerializeObject(settings));
    }

    private bool MetadataFileContainsRating(double targetRating)
    {
        string metadataPath = Path.Combine(_testHome, "track_metadata.json");
        if (!File.Exists(metadataPath))
            return false;

        var metadata = JsonConvert.DeserializeObject<Dictionary<string, TrackMetadata>>(File.ReadAllText(metadataPath));
        if (metadata == null || metadata.Count == 0)
            return false;

        return metadata.Values.Any(entry => Math.Abs(entry.Rating - targetRating) < 1);
    }
}
