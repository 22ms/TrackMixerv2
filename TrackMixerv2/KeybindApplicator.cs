using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

        // handledEventsToo: true so that global media keys (Space, arrow seek) reach
        // HandleMainWindowKey even when a child element such as a TabViewItem has
        // already consumed the event for its own navigation.  The handler itself
        // checks args.Handled before doing anything that could double-fire.
        window.RootGrid.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((sender, args) => HandleMainWindowKey(window, args)),
            true);

        window.RootGrid.AddHandler(
            UIElement.KeyUpEvent,
            new KeyEventHandler((sender, args) => HandleMainWindowKeyUp(window, args)),
            true);

        // Tab key interceptor — belt-and-suspenders alongside the GettingFocus guard below.
        window.RootGrid.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((_, args) =>
            {
                if (args.Key == VirtualKey.Tab)
                    args.Handled = true;
            }),
            true);

        // Blanket focus guard: cancel every focus transition except to text-input
        // controls (where the user needs to type) and ContentDialog descendants
        // (where WinUI needs focus to drive dialog keyboard behaviour).
        //
        // This is the most reliable way to stop any control — slider, button,
        // tab header, list item — from silently stealing keyboard focus on click,
        // without having to set AllowFocusOnInteraction on every element.
        // See BACKLOG.md for the plan to replace this with a proper focus model.
        FocusManager.GettingFocus += (_, e) =>
        {
            if (e.NewFocusedElement is TextBox or AutoSuggestBox or PasswordBox or NumberBox)
                return;

            if (IsInsideDialog(e.NewFocusedElement as DependencyObject))
                return;

            e.TryCancel();
        };

        _mainWindowHooked = true;
    }

    private static bool IsInsideDialog(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is ContentDialog)
                return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private static void HandleMainWindowKeyUp(MainWindow window, KeyRoutedEventArgs args)
    {
        if (window.ActiveMixerPage?.TryHandleKeybindRecordingKeyUp(args) != true)
            return;

        args.Handled = true;
    }

    public static void RemoveMixerPage(MixerPage page) => _hookedMixerPages.Remove(page);

    public static void ApplyToMixerPage(MixerPage page)
    {
        if (!_hookedMixerPages.Add(page))
            return;

        page.PageRootGrid.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((sender, args) => HandleMixerPageKey(page, args)),
            false);

        page.PageRootGrid.AddHandler(
            UIElement.KeyUpEvent,
            new KeyEventHandler((sender, args) => HandleMixerPageKeyUp(page, args)),
            true);
    }

    private static void HandleMixerPageKeyUp(MixerPage page, KeyRoutedEventArgs args)
    {
        if (!page.TryHandleKeybindRecordingKeyUp(args))
            return;

        args.Handled = true;
    }

    private static void HandleMainWindowKey(MainWindow window, KeyRoutedEventArgs args)
    {
        if (window.ActiveMixerPage?.TryHandleKeybindRecordingKeyDown(args) == true)
            return;

        if (TryHandleEscapeExitFullscreen(window, args))
            return;

        // Window-scoped actions (F11, tab management) only run when no child element
        // has already claimed the event and focus policy allows it.
        if (ShouldHandleKeybind(window.RootGrid, args))
        {
            if (TryHandleMainWindowAction(window, KeybindAction.ToggleFullscreen, args))
                return;
            if (TryHandleMainWindowAction(window, KeybindAction.NewTab, args))
                return;
            if (TryHandleMainWindowAction(window, KeybindAction.CloseTab, args))
                return;
            if (TryHandleMainWindowAction(window, KeybindAction.NextTab, args))
                return;
            if (TryHandleMainWindowAction(window, KeybindAction.PreviousTab, args))
                return;
        }

        // Media keys (PlayPause, Rewind, FastForward) are dispatched globally so they
        // work even when focus is on the TabView header or another non-MixerPage element.
        TryDispatchGlobalMediaKey(window, args);
    }

    /// <summary>
    /// Routes media keybinds to the active MixerPage using a permissive focus policy.
    /// This runs at the window level so keys work even when the TabView header has focus.
    /// </summary>
    private static void TryDispatchGlobalMediaKey(MainWindow window, KeyRoutedEventArgs args)
    {
        if (window.ActiveMixerPage == null || KeybindRecordingGate.IsRecording)
            return;

        if (IsModifierKey(args.Key))
            return;

        DependencyObject? focused = FocusManager.GetFocusedElement(window.RootGrid.XamlRoot) as DependencyObject;
        FocusedControlKind kind = focused == null
            ? FocusedControlKind.None
            : KeybindFocusPolicy.Classify(focused);

        int modifiers = KeybindChordCapture.GetCurrentModifiers();

        // If a child element already handled the event (e.g. the page-level keybind
        // handler dispatched PlayPause first), skip — the debounce in PlayPauseFromKeybind
        // would catch a double-dispatch anyway, but it is cleaner not to re-enter.
        if (args.Handled)
            return;

        if (KeybindFocusRules.ShouldDeferGlobalMediaKeybind(kind, (int)args.Key, modifiers))
            return;

        MixerPage page = window.ActiveMixerPage;
        if (TryHandleMixerPageAction(page, KeybindAction.PlayPause, args))
            return;
        if (TryHandleMixerPageAction(page, KeybindAction.Rewind, args))
            return;
        TryHandleMixerPageAction(page, KeybindAction.FastForward, args);
    }

    private static void HandleMixerPageKey(MixerPage page, KeyRoutedEventArgs args)
    {
        if (page.TryHandleKeybindRecordingKeyDown(args))
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

        if (KeybindRecordingGate.IsRecording)
            return false;

        if (IsModifierKey(args.Key))
            return false;

        DependencyObject? focused = FocusManager.GetFocusedElement(scope.XamlRoot) as DependencyObject;
        return !KeybindFocusPolicy.ShouldDeferKeybind(focused, args);
    }

    private static bool TryHandleEscapeExitFullscreen(MainWindow window, KeyRoutedEventArgs args)
    {
        if (args.Handled || args.Key != VirtualKey.Escape || KeybindRecordingGate.IsRecording)
            return false;

        if (!window.IsPlayerFullScreen)
            return false;

        window.ExitPlayerFullScreenFromKeybind(null!, null!);
        args.Handled = true;
        return true;
    }

    private static bool TryHandleMainWindowAction(MainWindow window, KeybindAction action, KeyRoutedEventArgs args)
    {
        if (!Matches(action, args))
            return false;

        switch (action)
        {
            case KeybindAction.ToggleFullscreen:
                window.TogglePlayerFullScreenFromKeybind(null!, null!);
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
