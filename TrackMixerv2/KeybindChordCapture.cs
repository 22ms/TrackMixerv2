using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace TrackMixerv2;

public static class KeybindChordCapture
{
  public static bool IsModifierKey(VirtualKey key) =>
      key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu
          or VirtualKey.LeftControl or VirtualKey.RightControl
          or VirtualKey.LeftShift or VirtualKey.RightShift
          or VirtualKey.LeftMenu or VirtualKey.RightMenu;

  public static int GetCurrentModifiers()
  {
    int modifiers = 0;
    if (IsKeyDown(VirtualKey.Shift) || IsKeyDown(VirtualKey.LeftShift) || IsKeyDown(VirtualKey.RightShift))
      modifiers |= 1;
    if (IsKeyDown(VirtualKey.Control) || IsKeyDown(VirtualKey.LeftControl) || IsKeyDown(VirtualKey.RightControl))
      modifiers |= 2;
    if (IsKeyDown(VirtualKey.Menu) || IsKeyDown(VirtualKey.LeftMenu) || IsKeyDown(VirtualKey.RightMenu))
      modifiers |= 4;
    return modifiers;
  }

  public static KeybindChord CreateChord(VirtualKey key) =>
      new((int)key, GetCurrentModifiers());

  private static bool IsKeyDown(VirtualKey key) =>
      InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
}
