using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class PathMatchingScenarioTests
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "TrackMixerScenarioTests", Guid.NewGuid().ToString("N"));

    public PathMatchingScenarioTests()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Videos"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "VideosBackup"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Videos", "NVIDIA", "Desktop"));
    }

    [Fact]
    public void Similar_root_folder_names_do_not_false_positive()
    {
        string videosClip = Path.Combine(_tempRoot, "Videos", "clip.mp4");
        string backupClip = Path.Combine(_tempRoot, "VideosBackup", "clip.mp4");

        Assert.True(Helper.PathIsUnderDirectory(videosClip, Path.Combine(_tempRoot, "Videos")));
        Assert.False(Helper.PathIsUnderDirectory(videosClip, Path.Combine(_tempRoot, "VideosBackup")));
        Assert.True(Helper.PathIsUnderDirectory(backupClip, Path.Combine(_tempRoot, "VideosBackup")));
        Assert.False(Helper.PathIsUnderDirectory(backupClip, Path.Combine(_tempRoot, "Videos")));
    }

    [Fact]
    public void Trailing_directory_separator_does_not_change_match()
    {
        string clip = Path.Combine(_tempRoot, "Videos", "clip.mp4");
        string withSlash = Path.Combine(_tempRoot, "Videos") + Path.DirectorySeparatorChar;

        Assert.True(Helper.PathIsUnderDirectory(clip, withSlash));
        Assert.True(Helper.PathIsUnderDirectory(clip, Path.Combine(_tempRoot, "Videos")));
    }

    [Fact]
    public void Nested_clip_still_matches_parent_root()
    {
        string nested = Path.Combine(_tempRoot, "Videos", "NVIDIA", "Desktop", "clip.mp4");

        Assert.True(Helper.PathIsUnderDirectory(nested, Path.Combine(_tempRoot, "Videos")));
    }

    [Fact]
    public void Parent_path_is_not_treated_as_child_file()
    {
        string root = Path.Combine(_tempRoot, "Videos");

        Assert.False(Helper.PathIsUnderDirectory(root, root));
    }
}
