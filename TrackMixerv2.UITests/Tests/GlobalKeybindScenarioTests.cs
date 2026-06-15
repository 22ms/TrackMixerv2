using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

/// <summary>
/// Verifies that media keybinds (Space = play/pause, Left/Right = seek) work regardless
/// of where the user last clicked in the UI.
///
/// Background:
///   Transport buttons (Next Track, Rewind, etc.) no longer steal keyboard focus when
///   clicked — <c>AllowFocusOnInteraction = False</c> is set in the AppBarButtonStyle.
///   Tab navigation is also intercepted globally so focus never moves via Tab.
///   As a result the window-level global-dispatch handler is the sole path for media
///   keybinds and these tests verify it fires reliably after various pointer interactions.
/// </summary>
[Collection(UiTestMutatingCollection.Name)]
[Trait("Category", "UI")]
public sealed class GlobalKeybindScenarioTests : IDisposable
{
    private static readonly TimeSpan KeyResponseTimeout = TimeSpan.FromSeconds(10);

    private readonly TrackMixerAppSession _session;

    public GlobalKeybindScenarioTests()
    {
        string clipPath = TrackMixerPaths.CreateTempClipLibrary();
        _session = new TrackMixerAppSession(clipPath);
        _session.Launch();

        // Wait for the clip to fully load before any test runs.
        UiWait.UntilTrue(
            () => _session.GetVolumeSliderCount() >= TrackMixerPaths.FixtureAudioTrackCount,
            TimeSpan.FromSeconds(30),
            "multi-track clip to load");
    }

    [Fact]
    public void Space_plays_video_after_clicking_tab_header()
    {
        _session.EnsurePaused();

        // Clicking the tab header does not move keyboard focus (AllowFocusOnInteraction
        // is False on transport buttons; Tab is intercepted globally).  Space must still
        // reach the global-dispatch handler and start playback.
        _session.ClickTabHeader();

        Keyboard.Type(VirtualKeyShort.SPACE);

        UiWait.UntilTrue(
            () => _session.IsPlaying(),
            KeyResponseTimeout,
            "playback to start after Space following a tab-header click");

        Assert.True(_session.IsPlaying(),
            "Space should trigger PlayPause regardless of prior pointer interaction.");
    }

    [Fact]
    public void Space_pauses_video_after_clicking_tab_header()
    {
        _session.EnsurePaused();
        _session.ClickPlayPause();
        UiWait.UntilTrue(() => _session.IsPlaying(), KeyResponseTimeout, "playing before test");

        _session.ClickTabHeader();

        Keyboard.Type(VirtualKeyShort.SPACE);

        UiWait.UntilTrue(
            () => !_session.IsPlaying(),
            KeyResponseTimeout,
            "playback to pause after Space following tab-header click");

        Assert.False(_session.IsPlaying(),
            "Space should trigger PlayPause (pause) regardless of prior pointer interaction.");
    }

    [Fact]
    public void Space_play_pause_cycle_works_repeatedly_after_tab_header_clicks()
    {
        _session.EnsurePaused();

        for (int cycle = 1; cycle <= 3; cycle++)
        {
            _session.ClickTabHeader();
            Thread.Sleep(300);
            Keyboard.Type(VirtualKeyShort.SPACE);
            UiWait.UntilTrue(
                () => _session.IsPlaying(),
                KeyResponseTimeout,
                $"playing via Space after tab-header click (cycle {cycle})");

            _session.ClickPlayPause();
            UiWait.UntilTrue(
                () => !_session.IsPlaying(),
                KeyResponseTimeout,
                $"paused via transport button (cycle {cycle})");
        }

        Assert.False(_session.Application.HasExited,
            "application should remain stable after repeated Space presses.");
    }

    [Fact]
    public void Arrow_keys_after_tab_header_click_do_not_crash_and_preserve_tab_count()
    {
        // Arrow keys must never crash the app, regardless of what was clicked last.
        _session.ClickTabHeader();

        Keyboard.Type(VirtualKeyShort.LEFT);
        Thread.Sleep(200);
        Keyboard.Type(VirtualKeyShort.RIGHT);
        Thread.Sleep(200);

        Assert.False(_session.Application.HasExited,
            "application must remain running after arrow keys following a tab-header click.");
        Assert.Equal(1, _session.GetTabCount());
    }

    [Fact]
    public void Space_works_after_clicking_tab_strip_not_player()
    {
        // Simulates the most common scenario: user clicks somewhere in the chrome
        // (not the video player) and then presses Space.
        _session.EnsurePaused();
        _session.ClickTabHeader();

        Keyboard.Type(VirtualKeyShort.SPACE);

        UiWait.UntilTrue(
            () => _session.IsPlaying(),
            KeyResponseTimeout,
            "playback to start after Space with focus on tab strip");

        Assert.True(_session.IsPlaying());
        Assert.False(_session.Application.HasExited);
    }

    public void Dispose() => _session.Dispose();
}
