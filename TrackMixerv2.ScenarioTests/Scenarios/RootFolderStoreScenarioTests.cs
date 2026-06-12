namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class RootFolderStoreScenarioTests : IDisposable
{
    readonly string? _previousRootFolders;
    readonly List<string>? _previousAppStateFolders;
    readonly bool _previousPersistToUserEnvironment;

    public RootFolderStoreScenarioTests()
    {
        _previousRootFolders = Environment.GetEnvironmentVariable(
            AppPaths.RootFoldersEnvVar, EnvironmentVariableTarget.User);
        _previousAppStateFolders = AppState.ROOT_FOLDERS;
        _previousPersistToUserEnvironment = RootFolderStore.PersistToUserEnvironment;
        AppState.Reset();
        AppState.ROOT_FOLDERS = new List<string>();
        RootFolderStore.PersistToUserEnvironment = false;
    }

    public void Dispose()
    {
        RootFolderStore.PersistToUserEnvironment = _previousPersistToUserEnvironment;
        Environment.SetEnvironmentVariable(
            AppPaths.RootFoldersEnvVar, _previousRootFolders, EnvironmentVariableTarget.User);
        AppState.ROOT_FOLDERS = _previousAppStateFolders;
    }

    [Fact]
    public void Add_normalizes_path_and_rejects_duplicates()
    {
        string folder = Path.Combine(Path.GetTempPath(), "TrackMixerRootStoreTest");
        Directory.CreateDirectory(folder);

        Assert.True(RootFolderStore.Add(folder));
        Assert.Single(RootFolderStore.Folders);
        Assert.Equal(Path.GetFullPath(folder), RootFolderStore.Folders[0]);

        Assert.False(RootFolderStore.Add(folder));
        Assert.Single(RootFolderStore.Folders);
    }

    [Fact]
    public void Remove_drops_folder_and_leaves_others()
    {
        string first = Path.Combine(Path.GetTempPath(), "TrackMixerRootA");
        string second = Path.Combine(Path.GetTempPath(), "TrackMixerRootB");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        RootFolderStore.Add(first);
        RootFolderStore.Add(second);
        Assert.Equal(2, RootFolderStore.Folders.Count);

        Assert.True(RootFolderStore.Remove(first));
        Assert.Single(RootFolderStore.Folders);
        Assert.Equal(Path.GetFullPath(second), RootFolderStore.Folders[0]);
    }

    [Fact]
    public void EnsureLoaded_strips_test_artifact_folders_from_environment()
    {
        string leaked = Path.Combine(Path.GetTempPath(), "TrackMixerRootB");
        string real = @"D:\Clips";
        Environment.SetEnvironmentVariable(
            AppPaths.RootFoldersEnvVar,
            $"{leaked};{real}",
            EnvironmentVariableTarget.Process);
        AppState.ROOT_FOLDERS = null;

        RootFolderStore.EnsureLoaded();

        Assert.Single(RootFolderStore.Folders);
        Assert.Equal(real, RootFolderStore.Folders[0]);
    }

    [Fact]
    public void IsKnownTestArtifactFolder_detects_temp_trackmixer_test_paths()
    {
        string testFolder = Path.Combine(Path.GetTempPath(), "TrackMixerRootB");
        Assert.True(RootFolderStore.IsKnownTestArtifactFolder(testFolder));
        Assert.False(RootFolderStore.IsKnownTestArtifactFolder(@"D:\Videos\NVIDIA"));
    }
}
