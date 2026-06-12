using Microsoft.UI.Input;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

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
        UIElement.PreviewKeyDownEvent,
        new KeyEventHandler((sender, args) => HandleMainWindowKey(window, args)),
        true);
    _mainWindowHooked = true;
  }

  public static void ApplyToMixerPage(MixerPage page)
  {
    if (!_hookedMixerPages.Add(page))
      return;

    page.PageRootGrid.AddHandler(
        UIElement.PreviewKeyDownEvent,
        new KeyEventHandler((sender, args) => HandleMixerPageKey(page, args)),
        true);
  }

  public static void RefreshAll()
  {
  }

  private static void HandleMainWindowKey(MainWindow window, KeyRoutedEventArgs args)
  {
    if (window.ActiveMixerPage?.TryHandleKeybindRecording(args) == true)
      return;

    if (Matches(KeybindAction.ExitFullscreen, args))
    {
      window.ExitPlayerFullScreenFromKeybind(null!, null!);
      args.Handled = true;
      return;
    }

    if (Matches(KeybindAction.NewTab, args))
    {
      window.NewTabFromKeybind(null!, null!);
      args.Handled = true;
      return;
    }

    if (Matches(KeybindAction.CloseTab, args))
    {
      window.CloseTabFromKeybind(null!, null!);
      args.Handled = true;
      return;
    }

    if (Matches(KeybindAction.NextTab, args))
    {
      window.NextTabFromKeybind(null!, null!);
      args.Handled = true;
      return;
    }

    if (Matches(KeybindAction.PreviousTab, args))
    {
      window.PreviousTabFromKeybind(null!, null!);
      args.Handled = true;
    }
  }

  private static void HandleMixerPageKey(MixerPage page, KeyRoutedEventArgs args)
  {
    if (page.TryHandleKeybindRecording(args))
      return;

    if (Matches(KeybindAction.PlayPause, args))
    {
      page.PlayPauseFromKeybind(null!, null!);
      args.Handled = true;
      return;
    }

    if (Matches(KeybindAction.Rewind, args))
    {
      page.RewindFromKeybind(null!, null!);
      args.Handled = true;
      return;
    }

    if (Matches(KeybindAction.FastForward, args))
    {
      page.FastForwardFromKeybind(null!, null!);
      args.Handled = true;
    }
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
