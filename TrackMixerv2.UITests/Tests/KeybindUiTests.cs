using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestMutatingCollection.Name)]
[Trait("Category", "UI")]
public sealed class KeybindUiTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly string _libraryRoot;
    private readonly TrackMixerAppSession _session;

    public KeybindUiTests()
    {
        string clip = TrackMixerPaths.CreateTempClipLibrary();
        _libraryRoot = Path.GetDirectoryName(clip)!;
        _session = new TrackMixerAppSession(clip, libraryRoot: _libraryRoot, deleteClipDirectoryOnDispose: false);
        _session.Launch();

        UiWait.UntilTrue(
            () => _session.MixerPageIsLoaded() && _session.GetKeybindShortcut("PlayPause") != null,
            TimeSpan.FromSeconds(25),
            "mixer page with keybind list");
    }

    [Fact]
    public void Default_playback_shortcuts_render_readable_labels()
    {
        Assert.Equal("Space", _session.GetKeybindShortcut("PlayPause"));
        Assert.Equal("Ctrl+W", _session.GetKeybindShortcut("CloseTab"));
        Assert.Equal("Shift", _session.GetKeybindShortcut("SpeedBoost"));
    }

    [Fact]
    public void Rebinding_play_pause_to_a_letter_updates_the_badge()
    {
        Assert.Equal("Space", _session.GetKeybindShortcut("PlayPause"));

        _session.ClickKeybindCell("PlayPause");
        Keyboard.Type(VirtualKeyShort.KEY_K);

        UiWait.UntilTrue(
            () => _session.GetKeybindShortcut("PlayPause") == "K",
            Timeout,
            "play/pause rebind to K");

        Assert.Equal("K", _session.GetKeybindShortcut("PlayPause"));
    }

    [Fact]
    public void Rebinding_speed_boost_supports_modifier_only_and_modifier_plus_key()
    {
        _session.ClickKeybindCell("SpeedBoost");
        Keyboard.Press(VirtualKeyShort.SHIFT);
        Thread.Sleep(100);
        Keyboard.Release(VirtualKeyShort.SHIFT);

        UiWait.UntilTrue(
            () => _session.GetKeybindShortcut("SpeedBoost") == "Shift",
            Timeout,
            "speed boost rebind to Shift only");
        Assert.Equal("Shift", _session.GetKeybindShortcut("SpeedBoost"));

        _session.ClickKeybindCell("SpeedBoost");
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_B);

        UiWait.UntilTrue(
            () => _session.GetKeybindShortcut("SpeedBoost") == "Ctrl+B",
            Timeout,
            "speed boost rebind to Ctrl+B");
        Assert.Equal("Ctrl+B", _session.GetKeybindShortcut("SpeedBoost"));
    }

    [Fact]
    public void Escape_cancels_recording_and_keeps_the_existing_binding()
    {
        string? before = _session.GetKeybindShortcut("Rewind");

        _session.ClickKeybindCell("Rewind");
        Keyboard.Type(VirtualKeyShort.ESCAPE);

        UiWait.UntilTrue(
            () => _session.GetKeybindShortcut("Rewind") == before,
            Timeout,
            "rewind binding restored after escape");
        Assert.Equal(before, _session.GetKeybindShortcut("Rewind"));

        _session.ClickKeybindCell("Rewind");
        Keyboard.Type(VirtualKeyShort.KEY_J);

        UiWait.UntilTrue(
            () => _session.GetKeybindShortcut("Rewind") == "J",
            Timeout,
            "rewind rebind works after a cancelled recording");
        Assert.Equal("J", _session.GetKeybindShortcut("Rewind"));
    }

    [Fact]
    public void Ctrl_w_shortcut_closes_the_active_tab()
    {
        Assert.True(_session.MixerPageIsLoaded());

        _session.MainWindow.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);

        UiWait.UntilTrue(
            () => !_session.MixerPageIsLoaded(),
            TimeSpan.FromSeconds(10),
            "tab to close after Ctrl+W");

        Assert.False(_session.MixerPageIsLoaded());
    }

    public void Dispose()
    {
        _session.Dispose();
        if (Directory.Exists(_libraryRoot))
            Directory.Delete(_libraryRoot, recursive: true);
    }
}
