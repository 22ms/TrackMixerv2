using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class RootFolderScenarioTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "TrackMixerScenarioTests", Guid.NewGuid().ToString("N"));

    public RootFolderScenarioTests()
    {
        AppState.Reset();
        Directory.CreateDirectory(_tempRoot);
        AppState.ROOT_FOLDERS = new List<string>
        {
            Path.Combine(_tempRoot, "Videos"),
            Path.Combine(_tempRoot, "VideosBackup")
        };
        Directory.CreateDirectory(AppState.ROOT_FOLDERS[0]);
        Directory.CreateDirectory(AppState.ROOT_FOLDERS[1]);
    }

    [Fact]
    public void Clip_in_similar_folder_name_does_not_attach_to_wrong_root()
    {
        string clipPath = Path.Combine(AppState.ROOT_FOLDERS[1], "clip.mp4");
        File.WriteAllText(clipPath, "scenario-test-clip");

        Assert.Equal(AppState.ROOT_FOLDERS[1], AppState.RootFoldersContainFile(clipPath));
        Assert.NotEqual(AppState.ROOT_FOLDERS[0], AppState.RootFoldersContainFile(clipPath));
    }

    [Fact]
    public void Clip_in_nested_subfolder_still_matches_configured_root()
    {
        string nested = Path.Combine(AppState.ROOT_FOLDERS[0], "NVIDIA", "Desktop", "clip.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(nested, "scenario-test-clip");

        Assert.Equal(AppState.ROOT_FOLDERS[0], AppState.RootFoldersContainFile(nested));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
        AppState.Reset();
    }
}
