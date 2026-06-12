using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestMutatingCollection.Name)]
[Trait("Category", "UI")]
public sealed class SkipTrackScenarioTests : IDisposable
{
    private readonly string _libraryRoot;
    private readonly TrackMixerAppSession _session;

    public SkipTrackScenarioTests()
    {
        (string multiTrackClip, _, string root) = TrackMixerPaths.CreateMixedTrackCountLibrary();
        _libraryRoot = root;
        _session = new TrackMixerAppSession(multiTrackClip, libraryRoot: root, deleteClipDirectoryOnDispose: false);
        _session.Launch();
    }

    [Fact]
    public void Skip_does_not_leak_audio_tracks()
    {
        UiWait.UntilTrue(
            () => _session.GetVolumeSliderCount() >= TrackMixerPaths.FixtureAudioTrackCount,
            TimeSpan.FromSeconds(30),
            "volume sliders for multi-track clip");

        Assert.Equal(TrackMixerPaths.FixtureAudioTrackCount, _session.GetVolumeSliderCount());

        _session.ClickNextTrack();

        UiWait.UntilTrue(
            () => _session.GetVolumeSliderCount() == TrackMixerPaths.SingleTrackFixtureAudioTrackCount
                && _session.VideoTitleContains(TrackMixerPaths.SingleTrackFixtureName),
            TimeSpan.FromSeconds(30),
            "volume sliders for single-track clip after skip");

        Assert.Equal(TrackMixerPaths.SingleTrackFixtureAudioTrackCount, _session.GetVolumeSliderCount());
        Assert.True(_session.VideoTitleContains(TrackMixerPaths.SingleTrackFixtureName));
        Assert.Null(_session.FindByAutomationId("VolumeSlider_1"));
    }

    public void Dispose()
    {
        _session.Dispose();
        if (Directory.Exists(_libraryRoot))
            Directory.Delete(_libraryRoot, recursive: true);
    }
}
