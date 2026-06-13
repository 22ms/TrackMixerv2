using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace TrackMixerv2;

public static class KeybindFocusPolicy
{
    public static bool ShouldDeferKeybind(DependencyObject? focused, KeyRoutedEventArgs args)
    {
        if (focused == null)
            return false;

        if (focused is TextBox { Name: "KeybindRecordingCapture" })
            return true;

        int modifiers = KeybindChordCapture.GetCurrentModifiers();
        FocusedControlKind kind = Classify(focused);
        return KeybindFocusRules.ShouldDeferToFocusedControl(kind, (int)args.Key, modifiers);
    }

    public static FocusedControlKind Classify(DependencyObject element)
    {
        if (FindAncestor<ContentDialog>(element) != null)
            return FocusedControlKind.Dialog;

        return element switch
        {
            TextBox or PasswordBox or NumberBox => FocusedControlKind.TextEntry,
            Slider => FocusedControlKind.Slider,
            Button or ToggleButton or DropDownButton or AppBarButton or AppBarToggleButton => FocusedControlKind.Button,
            MenuBar or MenuBarItem => FocusedControlKind.Menu,
            ComboBox => FocusedControlKind.ComboBox,
            ListView or ListViewItem or GridView or GridViewItem => FocusedControlKind.Selector,
            _ => FocusedControlKind.Other,
        };
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T match)
                return match;

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}
