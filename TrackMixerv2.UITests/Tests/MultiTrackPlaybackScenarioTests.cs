using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

/// <summary>
/// Verifies that the core multi-track playback flow behaves correctly end-to-end.
///
/// Each test creates its own isolated session so state never leaks between tests.
///
/// What cannot be verified automatically:
///   - That all auxiliary audio tracks are *audible* (requires human listening).
/// What IS verified:
///   - The app does not crash during play/pause on multi-track clips.
///   - State transitions (play → pause → play) succeed and the transport reflects them.
///   - Minimizing and restoring the window does not prevent subsequent playback.
///   - The correct number of tracks is present throughout the session.
/// </summary>
[Collection(UiTestMutatingCollection.Name)]
[Trait("Category", "UI")]
public sealed class MultiTrackPlaybackScenarioTests : IDisposable
{
    private readonly TrackMixerAppSession _session;

    public MultiTrackPlaybackScenarioTests()
    {
        string clipPath = TrackMixerPaths.CreateTempClipLibrary();
        _session = new TrackMixerAppSession(clipPath);
        _session.Launch();
    }

    [Fact]
    public void Play_pause_transport_works_without_crashing_on_multi_track_clip()
    {
        WaitForAllTracksLoaded();

        _session.EnsurePaused();

        _session.ClickPlayPause();
        UiWait.UntilTrue(
            () => _session.IsPlaying(),
            TimeSpan.FromSeconds(10),
            "playback to start");
        Assert.True(_session.IsPlaying(), "transport should report playing after clicking play");

        _session.ClickPlayPause();
        UiWait.UntilTrue(
            () => !_session.IsPlaying(),
            TimeSpan.FromSeconds(10),
            "playback to pause");
        Assert.False(_session.IsPlaying(), "transport should report paused after clicking pause");

        Assert.False(_session.Application.HasExited, "application must remain running after play/pause cycle");
        Assert.Equal(TrackMixerPaths.FixtureAudioTrackCount, _session.GetVolumeSliderCount());
    }

    [Fact]
    public void After_minimize_and_restore_play_resumes_without_crashing()
    {
        WaitForAllTracksLoaded();

        _session.EnsurePaused();

        // Simulate the application losing and regaining foreground focus.
        _session.MinimizeWindow();
        UiWait.UntilTrue(
            () => _session.MainWindow.Patterns.Window.Pattern.WindowVisualState.Value == WindowVisualState.Minimized,
            TimeSpan.FromSeconds(5),
            "window to minimize");
        _session.RestoreWindow();

        _session.ClickPlayPause();
        UiWait.UntilTrue(
            () => _session.IsPlaying(),
            TimeSpan.FromSeconds(10),
            "playback to resume after window restore");

        Assert.True(_session.IsPlaying(), "transport should report playing after restore + play");
        Assert.False(_session.Application.HasExited, "application must remain running after minimize/restore/play");
        Assert.Equal(TrackMixerPaths.FixtureAudioTrackCount, _session.GetVolumeSliderCount());
    }

    [Fact]
    public void Repeated_play_pause_cycles_remain_stable()
    {
        WaitForAllTracksLoaded();

        _session.EnsurePaused();

        for (int cycle = 1; cycle <= 5; cycle++)
        {
            _session.ClickPlayPause();
            UiWait.UntilTrue(
                () => _session.IsPlaying(),
                TimeSpan.FromSeconds(10),
                $"playing (cycle {cycle})");

            _session.ClickPlayPause();
            UiWait.UntilTrue(
                () => !_session.IsPlaying(),
                TimeSpan.FromSeconds(10),
                $"paused (cycle {cycle})");
        }

        Assert.False(_session.Application.HasExited, "application must survive five play/pause cycles");
        Assert.Equal(TrackMixerPaths.FixtureAudioTrackCount, _session.GetVolumeSliderCount());
    }

    [Fact]
    public void Seek_forward_then_play_keeps_every_track_on_the_new_position()
    {
        WaitForAllTracksLoaded();
        _session.EnsurePaused();
        double before = _session.GetDiagnostics()!.Position;

        _session.MainWindow.Focus();
        Keyboard.Type(VirtualKeyShort.RIGHT);
        _session.WaitForDiagnostics(
            d => d.Position >= before + LocalSettingsStore.DefaultSkipSeconds - 0.2,
            "skip forward");

        _session.ClickPlayPause();
        var playing = _session.WaitForDiagnostics(
            d => d.IsRunning && d.Tracks.All(t => t.State == "Playing"),
            "all tracks playing after the seek");

        Assert.True(playing.Spread < 0.05, $"tracks drifted apart: {playing}");
    }

    public void Dispose() => _session.Dispose();

    private void WaitForAllTracksLoaded()
    {
        UiWait.UntilTrue(
            () => _session.GetVolumeSliderCount() >= TrackMixerPaths.FixtureAudioTrackCount,
            TimeSpan.FromSeconds(30),
            $"{TrackMixerPaths.FixtureAudioTrackCount} volume sliders to confirm multi-track clip loaded");
    }
}
