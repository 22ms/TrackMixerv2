using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Support;

public sealed class ClipLibrary : IDisposable
{
    public string RootPath { get; }

    public ClipLibrary(string? suffix = null)
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "TrackMixerScenarioTests",
            suffix ?? Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string AddClip(string relativePath, DateTime creationTime)
    {
        string fullPath = Path.Combine(RootPath, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, "scenario-test-clip");
        File.SetCreationTime(fullPath, creationTime);
        return fullPath;
    }

    public void InstallAsRootFolder()
    {
        AppState.ROOT_FOLDERS = new List<string> { RootPath };
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
            Directory.Delete(RootPath, recursive: true);
    }
}
