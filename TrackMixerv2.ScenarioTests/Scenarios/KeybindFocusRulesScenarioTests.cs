using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class KeybindFocusRulesScenarioTests
{
    [Theory]
    [InlineData(FocusedControlKind.Slider, 0x25, 0, true)]
    [InlineData(FocusedControlKind.Slider, 0x27, 0, true)]
    [InlineData(FocusedControlKind.Slider, 0x20, 0, false)]
    [InlineData(FocusedControlKind.Slider, 0x25, 2, false)]
    [InlineData(FocusedControlKind.Button, 0x20, 0, true)]
    [InlineData(FocusedControlKind.Button, 0x0D, 0, true)]
    [InlineData(FocusedControlKind.Button, 0x25, 0, false)]
    [InlineData(FocusedControlKind.TextEntry, 0x41, 0, true)]
    [InlineData(FocusedControlKind.TextEntry, 0x41, 2, false)]
    [InlineData(FocusedControlKind.None, 0x20, 0, false)]
    public void ShouldDefer_matches_winui_native_keys(
        FocusedControlKind kind,
        int virtualKey,
        int modifiers,
        bool expected)
    {
        Assert.Equal(expected, KeybindFocusRules.ShouldDeferToFocusedControl(kind, virtualKey, modifiers));
    }

    [Theory]
    [InlineData(0x20, 0, true)]
    [InlineData(0x25, 0, true)]
    [InlineData(0x27, 0, true)]
    [InlineData(0x1B, 0, true)]
    [InlineData(0x09, 0, false)]
    [InlineData(0x09, 1, false)]
    [InlineData(0x09, 2, true)]
    [InlineData(0x79, 0, false)]
    [InlineData(0x79, 2, true)]
    public void TryValidateChord_allows_natural_shortcuts_but_blocks_accessibility_keys(
        int key,
        int modifiers,
        bool expectedValid)
    {
        bool valid = KeybindFocusRules.TryValidateChord(
            KeybindAction.PlayPause,
            new KeybindChord(key, modifiers),
            out string? error);

        Assert.Equal(expectedValid, valid);
        if (!expectedValid)
            Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void KeybindStore_TrySet_allows_arrow_keys_and_rejects_tab()
    {
        KeybindStore.ResetCache();

        Assert.True(KeybindStore.TrySet(KeybindAction.FastForward, new KeybindChord(0x27), out string? error));
        Assert.Null(error);
        Assert.Equal(0x27, KeybindStore.Get(KeybindAction.FastForward).Key);

        Assert.False(KeybindStore.TrySet(KeybindAction.PlayPause, new KeybindChord(0x09), out error));
        Assert.Contains("Tab", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0x09, 1, "Shift+Tab")]
    [InlineData(0x79, 0, "F10")]
    public void TryGetAccessibilityConflictMessage_blocks_navigation_reserved_keys(
        int key,
        int modifiers,
        string expectedFragment)
    {
        Assert.True(KeybindFocusRules.TryGetAccessibilityConflictMessage(
            new KeybindChord(key, modifiers),
            out string? error));
        Assert.Contains(expectedFragment, error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dialog_focus_defers_global_shortcuts_including_modified_chords()
    {
        Assert.True(KeybindFocusRules.ShouldDeferToFocusedControl(
            FocusedControlKind.Dialog,
            0x20,
            modifiers: 0));
        Assert.True(KeybindFocusRules.ShouldDeferToFocusedControl(
            FocusedControlKind.Dialog,
            0x20,
            modifiers: KeybindFocusRules.ModifierControl));
    }
}
