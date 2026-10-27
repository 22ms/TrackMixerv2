# Backlog

## Keyboard accessibility / tab navigation

**Status:** Disabled intentionally (see commit that added this file).

**What was disabled and why:**

WinUI's default tab-navigation moves keyboard focus to controls without a visible
focus indicator in all cases, causing confusing UX:

- Clicking a transport button (e.g. Next Track) silently gave that button keyboard
  focus. A subsequent Space press would then activate the button again instead of
  toggling play/pause.
- Pressing Tab without a highlighted focus ring made users unaware of where focus
  had landed, so arrow keys / Space triggered unexpected actions.

**Temporary mitigations applied:**

1. A `FocusManager.GettingFocus` guard in `KeybindApplicator` cancels every focus move
   except into text inputs, dialogs and flyout/menu content, so clicking a button or
   slider never takes keyboard focus.
2. Leaving a text input (e.g. the playlist NumberBox) moves focus to the inert
   `MainWindow.FocusSink` instead of being cancelled, which used to trap focus in the box
   and swallow every playback key until restart.
3. `DisableTabStops` sets `IsTabStop = false` on every `Control` in the TabView tree.
4. Tab key is intercepted at the `RootGrid` level in `KeybindApplicator` and marked
   handled, so focus never moves via Tab.

**What needs to be done properly:**

- Design a deliberate focus model: decide which controls should be reachable by
  keyboard, in what order, and how the focused state should be visually indicated.
- Let only the controls that should participate in keyboard navigation take focus.
- Re-enable Tab navigation (remove the interceptor in `KeybindApplicator`) once
  focus visuals are implemented.
- Consider replacing `DisableTabStops` with an explicit opt-in list of focusable
  controls rather than a blanket visual-tree walk.
- Add UI tests that verify the focus ring is visible on focusable elements.
