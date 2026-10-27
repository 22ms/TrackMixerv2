using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

/// <summary>
/// Regression tests for the reported playback bugs. They read the player state the app
/// publishes in UI-test mode (<see cref="PlaybackDiagnostics"/>) rather than counting
/// widgets. Players are muted in UI-test mode, so nothing is audible while these run.
/// </summary>
[Collection(UiTestMutatingCollection.Name)]
[Trait("Category", "UI")]
public sealed class PlaybackBehaviorUiTests : IDisposable
{
    private const int TransportRateIndexFor1_5 = 5;
    private readonly TrackMixerAppSession _session;
    private readonly string[] _clips;

    public PlaybackBehaviorUiTests()
    {
        (_clips, _) = TrackMixerPaths.CreateChronoClipLibrary(["a", "b", "c"]);
        _session = new TrackMixerAppSession(_clips[0]);
        _session.Launch();
        _session.WaitForDiagnostics(
            d => d.IsRunning && d.Tracks.Count == TrackMixerPaths.FixtureAudioTrackCount,
            "the clip to start playing with all tracks",
            seconds: 30);
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void Every_track_plays_audio_only_and_on_the_same_clock_as_the_video()
    {
        var diagnostics = _session.WaitForDiagnostics(
            d => d.Position > 2 && d.Tracks.All(t => t.State == "Playing"),
            "two seconds of playback on every track");

        Assert.Equal("Playing", diagnostics.Video?.State);
        Assert.All(diagnostics.Tracks, track => Assert.Equal("audio-only", track.Kind));
        Assert.True(diagnostics.Spread < 0.05, $"tracks drifted apart: {diagnostics}");
    }

    [Fact]
    public void Clicking_the_video_or_the_transport_bar_keeps_the_picked_speed()
    {
        PickSpeed(TransportRateIndexFor1_5);
        _session.WaitForDiagnostics(d => d.Rate == 1.5, "speed 1.5x");

        _session.ClickVideo();
        _session.Require("TimeElapsed").Click();
        _session.Require("TimeRemaining").Click();
        Wait.UntilInputIsProcessed();

        Assert.Equal(1.5, _session.GetDiagnostics()!.Rate);
    }

    [Fact]
    public void Mouse_wheel_on_the_timeline_moves_by_the_step_size_in_seconds()
    {
        _session.EnsurePaused();
        _session.SetRangeValue("ProgressSlider", 10);
        _session.WaitForDiagnostics(d => Math.Abs(d.Position - 10) < 0.2, "seek to 10 s");

        _session.ScrollOver("ProgressSlider", 1);
        _session.WaitForDiagnostics(
            d => Math.Abs(d.Position - (10 + LocalSettingsStore.DefaultSliderStepSize)) < 0.2,
            "one notch forward to move by the step size");

        _session.ScrollOver("ProgressSlider", -2);
        _session.WaitForDiagnostics(
            d => Math.Abs(d.Position - (10 - LocalSettingsStore.DefaultSliderStepSize)) < 0.2,
            "two notches back");
    }

    [Fact]
    public void Playback_keys_work_again_after_typing_in_the_playlist_number_box()
    {
        _session.Require("PlaylistFilterDropdown").AsButton().Invoke();
        _session.Require("PlaylistFilterRating").Click();
        var numberBox = _session.Require("PlaylistFilterTimeValue");
        numberBox.Click();
        Keyboard.Type("2");

        _session.ClickVideo();
        bool wasPlaying = _session.IsPlaying();
        Keyboard.Type(VirtualKeyShort.SPACE);

        UiWait.UntilTrue(
            () => _session.IsPlaying() != wasPlaying,
            TimeSpan.FromSeconds(5),
            "Space to toggle playback after leaving the number box");
    }

    [Fact]
    public void Holding_ctrl_for_a_shortcut_does_not_trigger_slow_motion()
    {
        double? rateDuringChord = null;
        _session.HoldKey(VirtualKeyShort.CONTROL, TimeSpan.FromMilliseconds(100), () =>
        {
            Keyboard.Type(VirtualKeyShort.NEXT);
            Thread.Sleep(KeybindHoldRules.ModifierOnlyActivationDelay * 3);
            rateDuringChord = _session.GetDiagnostics()?.Rate;
        });

        Assert.Equal(1.0, rateDuringChord);
    }

    [Fact]
    public void Hold_keys_change_speed_and_restore_the_picked_speed()
    {
        PickSpeed(TransportRateIndexFor1_5);
        _session.WaitForDiagnostics(d => d.Rate == 1.5, "speed 1.5x");
        _session.ClickVideo();

        double? rateWhileHeld = null;
        _session.HoldKey(VirtualKeyShort.CONTROL, TimeSpan.FromMilliseconds(100), () =>
        {
            Thread.Sleep(KeybindHoldRules.ModifierOnlyActivationDelay * 3);
            rateWhileHeld = _session.GetDiagnostics()?.Rate;
        });

        Assert.Equal(LocalSettingsStore.DefaultSpeedSlowRate, rateWhileHeld);
        _session.WaitForDiagnostics(d => d.Rate == 1.5, "the picked speed to come back after release");
    }

    [Fact]
    public void Next_clip_opens_with_every_track_playing_in_sync()
    {
        _session.ClickNextTrack();

        var diagnostics = _session.WaitForDiagnostics(
            d => d.Path == _clips[1] && d.Position > 1 && d.Tracks.All(t => t.State == "Playing"),
            "the next clip to play on every track");

        Assert.Equal(TrackMixerPaths.FixtureAudioTrackCount, diagnostics.Tracks.Count);
        Assert.True(diagnostics.Spread < 0.05, $"tracks drifted apart: {diagnostics}");
        Assert.Equal(TrackMixerPaths.FixtureAudioTrackCount, _session.GetVolumeSliderCount());
    }

    [Fact]
    public void Autoplay_advances_exactly_one_clip_even_after_toggling_fullscreen()
    {
        // Each fullscreen toggle re-parents the player, which used to subscribe its
        // MediaEnded handler again, so one clip end opened the next clip twice.
        for (int i = 0; i < 2; i++)
        {
            Keyboard.Type(VirtualKeyShort.F11);
            Wait.UntilInputIsProcessed();
            Keyboard.Type(VirtualKeyShort.F11);
            Wait.UntilInputIsProcessed();
        }

        _session.Require("AutoplayButton").AsButton().Invoke();
        _session.Require("AutoplayForwardOption").Click();
        _session.SetRangeValue("ProgressSlider", TrackMixerPaths.FixtureDurationSeconds - 1);

        _session.WaitForDiagnostics(d => d.Path == _clips[1] && d.IsRunning, "autoplay to open the next clip", seconds: 20);
        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.Equal(_clips[1], _session.GetDiagnostics()!.Path);
    }

    private void PickSpeed(int rateIndex)
    {
        _session.Require("SpeedSelectButton").AsButton().Invoke();
        _session.SetRangeValue("PlaybackRateSlider", rateIndex);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Wait.UntilInputIsProcessed();
    }
}
