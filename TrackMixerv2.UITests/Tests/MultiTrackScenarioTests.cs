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
    }
}
