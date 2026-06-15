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

1. `AllowFocusOnInteraction = False` on all transport `AppBarButton` /
   `AppBarToggleButton` controls — clicking a button no longer steals focus.
2. `DisableTabStops` also sets `AllowFocusOnInteraction = false` on every `Control`
   in the visual tree when a tab or video loads.
3. Tab key is intercepted at the `RootGrid` level in `KeybindApplicator` and marked
   handled, so focus never moves via Tab.

**What needs to be done properly:**

- Design a deliberate focus model: decide which controls should be reachable by
  keyboard, in what order, and how the focused state should be visually indicated.
- Re-enable `AllowFocusOnInteraction = True` only for the controls that should
  participate in keyboard navigation.
- Re-enable Tab navigation (remove the interceptor in `KeybindApplicator`) once
  focus visuals are implemented.
- Consider replacing `DisableTabStops` with an explicit opt-in list of focusable
  controls rather than a blanket visual-tree walk.
- Add UI tests that verify the focus ring is visible on focusable elements.
