using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace TrackMixerv2;

public static class KeybindApplicator
{
    private static bool _mainWindowHooked;
    private static readonly HashSet<MixerPage> _hookedMixerPages = new();

    public static void ApplyToMainWindow(MainWindow window)
    {
        if (_mainWindowHooked)
            return;

        window.RootGrid.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((sender, args) => HandleMainWindowKey(window, args)),
            false);
        _mainWindowHooked = true;
    }

    public static void ApplyToMixerPage(MixerPage page)
    {
        if (!_hookedMixerPages.Add(page))
            return;

        page.PageRootGrid.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((sender, args) => HandleMixerPageKey(page, args)),
            false);
    }

    private static void HandleMainWindowKey(MainWindow window, KeyRoutedEventArgs args)
    {
        if (window.ActiveMixerPage?.TryHandleKeybindRecording(args) == true)
            return;

        if (!ShouldHandleKeybind(window.RootGrid, args))
            return;

        if (TryHandleMainWindowAction(window, KeybindAction.ExitFullscreen, args))
            return;
        if (TryHandleMainWindowAction(window, KeybindAction.NewTab, args))
            return;
        if (TryHandleMainWindowAction(window, KeybindAction.CloseTab, args))
            return;
        if (TryHandleMainWindowAction(window, KeybindAction.NextTab, args))
            return;
        TryHandleMainWindowAction(window, KeybindAction.PreviousTab, args);
    }

    private static void HandleMixerPageKey(MixerPage page, KeyRoutedEventArgs args)
    {
        if (page.TryHandleKeybindRecording(args))
            return;

        if (!ShouldHandleKeybind(page.PageRootGrid, args))
            return;

        if (TryHandleMixerPageAction(page, KeybindAction.PlayPause, args))
            return;
        if (TryHandleMixerPageAction(page, KeybindAction.Rewind, args))
            return;
        TryHandleMixerPageAction(page, KeybindAction.FastForward, args);
    }

    private static bool ShouldHandleKeybind(UIElement scope, KeyRoutedEventArgs args)
    {
        if (args.Handled)
            return false;

        if (IsModifierKey(args.Key))
            return false;

        DependencyObject? focused = FocusManager.GetFocusedElement(scope.XamlRoot) as DependencyObject;
        return !KeybindFocusPolicy.ShouldDeferKeybind(focused, args);
    }

    private static bool TryHandleMainWindowAction(MainWindow window, KeybindAction action, KeyRoutedEventArgs args)
    {
        if (!Matches(action, args))
            return false;

        switch (action)
        {
            case KeybindAction.ExitFullscreen:
                window.ExitPlayerFullScreenFromKeybind(null!, null!);
                break;
            case KeybindAction.NewTab:
                window.NewTabFromKeybind(null!, null!);
                break;
            case KeybindAction.CloseTab:
                window.CloseTabFromKeybind(null!, null!);
                break;
            case KeybindAction.NextTab:
                window.NextTabFromKeybind(null!, null!);
                break;
            case KeybindAction.PreviousTab:
                window.PreviousTabFromKeybind(null!, null!);
                break;
        }

        args.Handled = true;
        return true;
    }

    private static bool TryHandleMixerPageAction(MixerPage page, KeybindAction action, KeyRoutedEventArgs args)
    {
        if (!Matches(action, args))
            return false;

        switch (action)
        {
            case KeybindAction.PlayPause:
                page.PlayPauseFromKeybind(null!, null!);
                break;
            case KeybindAction.Rewind:
                page.RewindFromKeybind(null!, null!);
                break;
            case KeybindAction.FastForward:
                page.FastForwardFromKeybind(null!, null!);
                break;
        }

        args.Handled = true;
        return true;
    }

    internal static bool Matches(KeybindAction action, KeyRoutedEventArgs args)
    {
        if (IsModifierKey(args.Key))
            return false;

        KeybindChord chord = KeybindStore.Get(action);
        return (int)args.Key == chord.Key && KeybindChordCapture.GetCurrentModifiers() == chord.Modifiers;
    }

    private static bool IsModifierKey(VirtualKey key) =>
        KeybindChordCapture.IsModifierKey(key);
}
