using FlaUI.Core.AutomationElements;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UI")]
public sealed class MultiTrackScenarioTests
{
    private readonly SharedUiAppFixture _fixture;

    public MultiTrackScenarioTests(SharedUiAppFixture fixture) => _fixture = fixture;

    [Fact]
    public void Clip_loads_one_volume_slider_per_audio_track()
    {
        UiWait.UntilTrue(
            () => _fixture.Session.GetVolumeSliderCount() >= TrackMixerPaths.FixtureAudioTrackCount,
            TimeSpan.FromSeconds(25),
            "volume sliders for each audio track");

        Assert.Equal(TrackMixerPaths.FixtureAudioTrackCount, _fixture.Session.GetVolumeSliderCount());
    }

    [Fact]
    public void User_can_adjust_individual_track_volumes()
    {
        UiWait.UntilTrue(
            () => _fixture.Session.FindByAutomationId("VolumeSlider_1") != null,
            TimeSpan.FromSeconds(25),
            "second track volume slider");

        var track0 = _fixture.Session.FindByAutomationId("VolumeSlider_0")!.AsSlider();
        var track2 = _fixture.Session.FindByAutomationId("VolumeSlider_2")!.AsSlider();

        track0.Value = 20;
        track2.Value = 80;

        UiWait.UntilTrue(
            () => Math.Abs(track0.Value - 20) < 1 && Math.Abs(track2.Value - 80) < 1,
            TimeSpan.FromSeconds(5),
            "per-track volume values");

        Assert.InRange(track0.Value, 19, 21);
        Assert.InRange(track2.Value, 79, 81);

        // Each slider must drive its own track's player, whatever order the players opened in.
        var diagnostics = _fixture.Session.WaitForDiagnostics(
            d => d.Tracks.Count == TrackMixerPaths.FixtureAudioTrackCount
                && Math.Abs((d.Tracks[0].Volume ?? -1) - 0.2) < 0.01
                && Math.Abs((d.Tracks[2].Volume ?? -1) - 0.8) < 0.01,
            "player volumes to follow their sliders");
        Assert.Equal(1.0, diagnostics.Tracks[1].Volume ?? -1, 2);
        Assert.Equal(1.0, diagnostics.Tracks[3].Volume ?? -1, 2);

        track0.Value = VolumeScale.ToPosition(160);
        _fixture.Session.WaitForDiagnostics(
            d => Math.Abs((d.Tracks[0].Volume ?? -1) - 1.6) < 0.01,
            "track 0 to be boosted to 160%");
    }
}
