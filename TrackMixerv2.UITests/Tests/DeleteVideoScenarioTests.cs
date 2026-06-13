using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestMutatingCollection.Name)]
[Trait("Category", "UI")]
public sealed class DeleteVideoScenarioTests : IDisposable
{
    private readonly string _libraryRoot;
    private readonly TrackMixerAppSession _session;
    private readonly string[] _clips;

    public DeleteVideoScenarioTests()
    {
        (_clips, _libraryRoot) = TrackMixerPaths.CreateChronoClipLibrary(
            ["first.mp4", "middle.mp4", "latest.mp4"]);
        string latest = _clips[^1];

        _session = new TrackMixerAppSession(latest, libraryRoot: _libraryRoot, deleteClipDirectoryOnDispose: false);
        _session.Launch();

        UiWait.UntilTrue(
            () => _session.MixerPageIsLoaded() && _session.VideoTitleContains("latest.mp4"),
            TimeSpan.FromSeconds(30),
            "mixer page to load latest clip");
    }

    [Fact]
    public void Deleting_clips_navigates_to_neighbor_and_keeps_tab_open()
    {
        Assert.Equal(1, _session.GetTabCount());
        Assert.True(File.Exists(_clips[2]));

        _session.ConfirmDeleteCurrentVideo();

        UiWait.UntilTrue(
            () => !File.Exists(_clips[2])
                && File.Exists(_clips[1])
                && _session.VideoTitleContains("middle.mp4")
                && _session.MixerPageIsLoaded(),
            TimeSpan.FromSeconds(30),
            "navigation to middle clip after deleting latest");

        Assert.Equal(1, _session.GetTabCount());

        _session.ConfirmDeleteCurrentVideo();

        UiWait.UntilTrue(
            () => !File.Exists(_clips[1])
                && File.Exists(_clips[0])
                && _session.VideoTitleContains("first.mp4")
                && _session.MixerPageIsLoaded(),
            TimeSpan.FromSeconds(30),
            "navigation to first clip after deleting middle");

        Assert.Equal(1, _session.GetTabCount());

        _session.ConfirmDeleteCurrentVideo();

        UiWait.UntilTrue(
            () => !File.Exists(_clips[0]) && _session.MixerPageIsLoaded(),
            TimeSpan.FromSeconds(30),
            "tab to remain open after deleting last clip");

        Assert.Equal(1, _session.GetTabCount());
        Assert.False(File.Exists(_clips[0]));
    }

    public void Dispose()
    {
        _session.Dispose();
        if (Directory.Exists(_libraryRoot))
            Directory.Delete(_libraryRoot, recursive: true);
    }
}
