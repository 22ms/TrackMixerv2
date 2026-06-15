namespace TrackMixerv2;

public enum FocusedControlKind
{
    None,
    TextEntry,
    Slider,
    Button,
    Menu,
    ComboBox,
    Selector,
    Dialog,
    Other,
}

public static class KeybindFocusRules
{
    public const int ModifierShift = 1;
    public const int ModifierControl = 2;
    public const int ModifierAlt = 4;

    public static bool ShouldDeferToFocusedControl(FocusedControlKind kind, int virtualKey, int modifiers)
    {
        if (kind is FocusedControlKind.None or FocusedControlKind.Other)
            return false;

        if (kind == FocusedControlKind.Dialog)
            return true;

        if (modifiers != 0)
            return false;

        return kind switch
        {
            FocusedControlKind.TextEntry => true,
            FocusedControlKind.Slider => IsSliderNavigationKey(virtualKey),
            FocusedControlKind.Menu => IsMenuNavigationKey(virtualKey),
            FocusedControlKind.ComboBox => IsComboBoxNavigationKey(virtualKey),
            FocusedControlKind.Selector => IsSelectorNavigationKey(virtualKey),
            _ => false,
        };
    }

    public static bool TryValidateChord(KeybindAction action, KeybindChord chord, out string? error)
    {
        error = null;

        if (action == KeybindAction.ToggleFullscreen
            && chord.Key == 0x1B
            && chord.Modifiers == 0)
        {
            error = "Escape always exits fullscreen and can't be reassigned. Choose another key for toggle fullscreen.";
            return false;
        }

        if (TryGetAccessibilityConflictMessage(chord, out error))
            return false;

        return true;
    }

    /// <summary>
    /// Keys that cannot work as shortcuts because Windows/WinUI reserves them for keyboard navigation.
    /// </summary>
    public static bool TryGetAccessibilityConflictMessage(KeybindChord chord, out string? error)
    {
        const int Tab = 0x09;
        const int F10 = 0x79;

        if (chord.Key == Tab && chord.Modifiers == 0)
        {
            error = "Tab moves focus between controls and can't be used as a shortcut. Choose another key or add a modifier (for example Ctrl+T).";
            return true;
        }

        if (chord.Key == Tab && chord.Modifiers == ModifierShift)
        {
            error = "Shift+Tab moves focus backward and can't be used as a shortcut. Choose another key combination.";
            return true;
        }

        if (chord.Key == F10 && chord.Modifiers == 0)
        {
            error = "F10 activates the menu bar and can't be used as a shortcut without a modifier.";
            return true;
        }

        error = null;
        return false;
    }

    /// <summary>
    /// Whether a global (window-level) media keybind should defer to the focused control.
    ///
    /// More permissive than <see cref="ShouldDeferToFocusedControl"/>: only text entry
    /// controls and modal dialogs block media keys; sliders still own their arrow keys.
    /// </summary>
    public static bool ShouldDeferGlobalMediaKeybind(FocusedControlKind kind, int virtualKey, int modifiers)
    {
        // Modal dialogs always win, even with modifiers.
        if (kind == FocusedControlKind.Dialog)
            return true;

        // Modified chords (Ctrl/Alt/Shift+key) are user-defined shortcuts; they pass
        // through even from text boxes.
        if (modifiers != 0)
            return false;

        // Bare keys in a text box belong to the text box.
        if (kind == FocusedControlKind.TextEntry)
            return true;

        return false;
    }

    public static bool IsSliderNavigationKey(int virtualKey) =>
        virtualKey is 0x25 or 0x26 or 0x27 or 0x28 or 0x21 or 0x22 or 0x24 or 0x23;

    public static bool IsButtonActivationKey(int virtualKey) =>
        virtualKey is 0x20 or 0x0D;

    public static bool IsMenuNavigationKey(int virtualKey) =>
        virtualKey is 0x25 or 0x26 or 0x27 or 0x28 or 0x20 or 0x0D or 0x1B;

    public static bool IsComboBoxNavigationKey(int virtualKey) =>
        virtualKey is 0x25 or 0x26 or 0x27 or 0x28 or 0x20 or 0x0D or 0x1B;

    public static bool IsSelectorNavigationKey(int virtualKey) =>
        virtualKey is 0x25 or 0x26 or 0x27 or 0x28 or 0x21 or 0x22 or 0x24 or 0x23 or 0x20 or 0x0D;
}
