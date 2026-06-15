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
    // Buttons can no longer receive keyboard focus (AllowFocusOnInteraction = False),
    // so the Button activation case was removed from ShouldDeferToFocusedControl.
    [InlineData(FocusedControlKind.Button, 0x20, 0, false)]
    [InlineData(FocusedControlKind.Button, 0x0D, 0, false)]
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

    // -------------------------------------------------------------------------
    // ShouldDeferGlobalMediaKeybind – window-level permissive policy
    // -------------------------------------------------------------------------

    [Theory]
    // TextEntry always defers
    [InlineData(FocusedControlKind.TextEntry, 0x20, 0, true)]
    [InlineData(FocusedControlKind.TextEntry, 0x27, 0, true)]
    // Dialog always defers
    [InlineData(FocusedControlKind.Dialog, 0x20, 0, true)]
    [InlineData(FocusedControlKind.Dialog, 0x27, 2, true)]
    // Sliders can no longer receive focus (AllowFocusOnInteraction=False), so the
    // Slider branch was removed from ShouldDeferGlobalMediaKeybind — all keys pass through.
    [InlineData(FocusedControlKind.Slider, 0x25, 0, false)]  // Left arrow passes through
    [InlineData(FocusedControlKind.Slider, 0x27, 0, false)]  // Right arrow passes through
    [InlineData(FocusedControlKind.Slider, 0x20, 0, false)]  // Space passes through
    // Selector: Tab is now intercepted globally and AllowFocusOnInteraction is False on
    // transport buttons, so selectors can no longer receive focus.  The Selector branch
    // was removed from ShouldDeferGlobalMediaKeybind — all keys pass through.
    [InlineData(FocusedControlKind.Selector, 0x25, 0, false)] // Left arrow passes through
    [InlineData(FocusedControlKind.Selector, 0x27, 0, false)] // Right arrow passes through
    [InlineData(FocusedControlKind.Selector, 0x20, 0, false)] // Space passes through
    // Button, Other, None: media keys always pass through
    [InlineData(FocusedControlKind.Button, 0x20, 0, false)]
    [InlineData(FocusedControlKind.Button, 0x25, 0, false)]
    [InlineData(FocusedControlKind.Other, 0x20, 0, false)]
    [InlineData(FocusedControlKind.None, 0x27, 0, false)]
    // Modified chords always pass through regardless of focus kind
    [InlineData(FocusedControlKind.Selector, 0x27, 2, false)]
    [InlineData(FocusedControlKind.TextEntry, 0x27, 2, false)]
    public void ShouldDeferGlobalMediaKeybind_applies_permissive_policy(
        FocusedControlKind kind,
        int virtualKey,
        int modifiers,
        bool expectedDefer)
    {
        Assert.Equal(expectedDefer,
            KeybindFocusRules.ShouldDeferGlobalMediaKeybind(kind, virtualKey, modifiers));
    }

    [Fact]
    public void ShouldDeferGlobalMediaKeybind_local_policy_still_defers_selector_space_even_though_global_does_not()
    {
        const int space = 0x20;

        // Local (page-level) policy still defers Space on a Selector via
        // IsSelectorNavigationKey — this is fine because Selectors can no longer
        // receive keyboard focus (Tab intercepted, AllowFocusOnInteraction = False).
        Assert.True(KeybindFocusRules.ShouldDeferToFocusedControl(
            FocusedControlKind.Selector, space, modifiers: 0));

        // Global (window-level) policy no longer defers Selectors at all — the
        // Selector branch was removed since selectors cannot get focus.
        Assert.False(KeybindFocusRules.ShouldDeferGlobalMediaKeybind(
            FocusedControlKind.Selector, space, modifiers: 0));
    }
}
