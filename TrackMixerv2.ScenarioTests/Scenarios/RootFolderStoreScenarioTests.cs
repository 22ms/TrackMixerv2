using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class RootFolderStoreScenarioTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TrackMixerScenarioTests", Guid.NewGuid().ToString("N"));
    private readonly string? _previousProcessValue = Environment.GetEnvironmentVariable(AppPaths.RootFoldersEnvVar);

    public RootFolderStoreScenarioTests()
    {
        AppState.Reset();
        AppState.ROOT_FOLDERS = new List<string>();
        RootFolderStore.PersistTarget = EnvironmentVariableTarget.Process;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        RootFolderStore.FlushAsync().Wait();
        RootFolderStore.PersistTarget = EnvironmentVariableTarget.User;
        Environment.SetEnvironmentVariable(AppPaths.RootFoldersEnvVar, _previousProcessValue);
        AppState.Reset();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Add_normalizes_path_and_rejects_duplicates()
    {
        string folder = Folder("Clips");

        Assert.True(RootFolderStore.Add(folder + Path.DirectorySeparatorChar));
        Assert.Equal(Path.GetFullPath(folder), Assert.Single(RootFolderStore.Folders));

        Assert.False(RootFolderStore.Add(folder.ToUpperInvariant()));
        Assert.Single(RootFolderStore.Folders);
    }

    [Fact]
    public void Remove_drops_folder_and_leaves_others()
    {
        string first = Folder("A");
        string second = Folder("B");
        RootFolderStore.Add(first);
        RootFolderStore.Add(second);

        Assert.True(RootFolderStore.Remove(first));
        Assert.Equal(Path.GetFullPath(second), Assert.Single(RootFolderStore.Folders));
        Assert.False(RootFolderStore.Remove(first));
    }

    [Fact]
    public async Task Rapid_changes_persist_in_order_so_the_last_one_wins()
    {
        string first = Folder("A");
        string second = Folder("B");

        RootFolderStore.Add(first);
        RootFolderStore.Remove(first);
        RootFolderStore.Add(second);
        await RootFolderStore.FlushAsync();

        Assert.Equal(Path.GetFullPath(second), Environment.GetEnvironmentVariable(AppPaths.RootFoldersEnvVar));

        RootFolderStore.Remove(second);
        await RootFolderStore.FlushAsync();
        Assert.Null(Environment.GetEnvironmentVariable(AppPaths.RootFoldersEnvVar));
    }

    [Fact]
    public void Changes_replace_the_list_instead_of_mutating_it()
    {
        IReadOnlyList<string> before = RootFolderStore.Folders;

        RootFolderStore.Add(Folder("A"));

        Assert.Empty(before);
        Assert.Single(RootFolderStore.Folders);
    }

    [Fact]
    public void EnsureLoaded_reads_semicolon_separated_folders()
    {
        Environment.SetEnvironmentVariable(AppPaths.RootFoldersEnvVar, @"D:\Clips; E:\Replays ;");
        AppState.ROOT_FOLDERS = null;

        RootFolderStore.EnsureLoaded();

        Assert.Equal(new[] { @"D:\Clips", @"E:\Replays" }, RootFolderStore.Folders);
    }

    private string Folder(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
