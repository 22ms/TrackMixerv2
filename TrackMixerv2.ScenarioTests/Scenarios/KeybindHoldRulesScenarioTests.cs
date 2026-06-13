using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class KeybindHoldRulesScenarioTests
{
    [Theory]
    [InlineData(KeybindAction.SpeedBoost, true)]
    [InlineData(KeybindAction.SpeedSlow, true)]
    [InlineData(KeybindAction.PlayPause, false)]
    [InlineData(KeybindAction.Rewind, false)]
    public void IsHoldAction_identifies_hold_speed_keybinds(KeybindAction action, bool expected)
    {
        Assert.Equal(expected, KeybindHoldRules.IsHoldAction(action));
    }

    [Theory]
    [InlineData(KeybindHoldRules.GenericShift)]
    [InlineData(KeybindHoldRules.GenericControl)]
    [InlineData(KeybindHoldRules.GenericMenu)]
    [InlineData(0xA0)]
    [InlineData(0xA3)]
    public void IsModifierVirtualKey_includes_winui_generic_modifier_codes(int virtualKey)
    {
        Assert.True(KeybindHoldRules.IsModifierVirtualKey(virtualKey));
    }

    [Theory]
    [InlineData(KeybindHoldRules.GenericShift, 0xA0)]
    [InlineData(0xA1, 0xA0)]
    [InlineData(KeybindHoldRules.GenericControl, 0xA2)]
    [InlineData(0xA3, 0xA2)]
    [InlineData(KeybindHoldRules.GenericMenu, 0xA4)]
    [InlineData(0xA5, 0xA4)]
    [InlineData(0x42, 0x42)]
    public void NormalizeModifierKey_maps_generic_and_side_specific_modifiers(int input, int expected)
    {
        Assert.Equal(expected, KeybindHoldRules.NormalizeModifierKey(input));
    }

    [Theory]
    [InlineData(KeybindHoldRules.GenericShift, 0xA1, true)]
    [InlineData(0xA0, KeybindHoldRules.GenericShift, true)]
    [InlineData(KeybindHoldRules.GenericControl, 0xA3, true)]
    [InlineData(KeybindHoldRules.GenericShift, KeybindHoldRules.GenericControl, false)]
    public void ModifierKeysMatch_treats_generic_and_side_specific_keys_as_same_modifier(
        int firstKey,
        int secondKey,
        bool expected)
    {
        Assert.Equal(expected, KeybindHoldRules.ModifierKeysMatch(firstKey, secondKey));
    }

    [Theory]
    [InlineData(0xA0, 0xA0, true)]
    [InlineData(0xA0, 0xA1, true)]
    [InlineData(0xA0, KeybindHoldRules.GenericShift, true)]
    [InlineData(0xA0, 0xA2, false)]
    public void IsChordActive_matches_modifier_only_hold_on_either_side(
        int boundKey,
        int pressedKey,
        bool expected)
    {
        var chord = new KeybindChord(boundKey, 0);

        bool active = KeybindHoldRules.IsChordActive(
            chord,
            key => key == pressedKey,
            () => 0);

        Assert.Equal(expected, active);
    }

    [Fact]
    public void IsChordActive_matches_modifier_plus_key_combo()
    {
        var chord = new KeybindChord(0x53, KeybindFocusRules.ModifierControl);

        Assert.True(KeybindHoldRules.IsChordActive(
            chord,
            key => key == 0x53,
            () => KeybindFocusRules.ModifierControl));
    }

    [Fact]
    public void IsChordActive_modifier_plus_key_requires_matching_modifiers()
    {
        var chord = new KeybindChord(0x53, KeybindFocusRules.ModifierShift);

        Assert.False(KeybindHoldRules.IsChordActive(
            chord,
            key => key == 0x53,
            () => KeybindFocusRules.ModifierControl));
    }

    [Fact]
    public void IsChordActive_modifier_plus_key_requires_primary_key_down()
    {
        var chord = new KeybindChord(0x53, KeybindFocusRules.ModifierShift);

        Assert.False(KeybindHoldRules.IsChordActive(
            chord,
            _ => false,
            () => KeybindFocusRules.ModifierShift));
    }

    [Fact]
    public void KeybindStore_accepts_modifier_only_and_modifier_plus_key_for_hold_actions()
    {
        KeybindStore.ResetCache();

        Assert.True(KeybindStore.TrySet(
            KeybindAction.SpeedBoost,
            KeybindHoldRules.CreateModifierOnlyChord(KeybindHoldRules.GenericShift),
            out string? error));
        Assert.Null(error);
        Assert.Equal(0xA0, KeybindStore.Get(KeybindAction.SpeedBoost).Key);
        Assert.Equal(0, KeybindStore.Get(KeybindAction.SpeedBoost).Modifiers);

        Assert.True(KeybindStore.TrySet(
            KeybindAction.SpeedBoost,
            new KeybindChord(0x42, KeybindFocusRules.ModifierShift),
            out error));
        Assert.Null(error);
        Assert.Equal(0x42, KeybindStore.Get(KeybindAction.SpeedBoost).Key);
        Assert.Equal(KeybindFocusRules.ModifierShift, KeybindStore.Get(KeybindAction.SpeedBoost).Modifiers);
    }

    [Fact]
    public void Recording_shift_then_b_waits_for_non_modifier_and_does_not_finalize_on_shift_down()
    {
        var state = new KeybindHoldRecordingState();

        Assert.Equal(HoldRecordingKeyDownResult.ModifierPending, state.OnKeyDown(KeybindHoldRules.GenericShift));
        Assert.Equal(HoldRecordingKeyDownResult.NonModifierPressed, state.OnKeyDown(0x42));
        Assert.Null(state.TryFinalizeModifierOnlyOnKeyUp(KeybindHoldRules.GenericShift));
    }

    [Fact]
    public void Recording_shift_only_finalizes_normalized_modifier_on_keyup()
    {
        var state = new KeybindHoldRecordingState();
        state.OnKeyDown(KeybindHoldRules.GenericShift);

        KeybindChord? chord = state.TryFinalizeModifierOnlyOnKeyUp(0xA1);

        Assert.NotNull(chord);
        Assert.Equal(0xA0, chord!.Key);
        Assert.Equal(0, chord.Modifiers);
        Assert.Equal("Shift", KeybindFormatter.Format(chord));
    }

    [Fact]
    public void Format_modifier_only_shift_does_not_show_key16()
    {
        Assert.Equal("Shift", KeybindFormatter.Format(KeybindHoldRules.CreateModifierOnlyChord(KeybindHoldRules.GenericShift)));
        Assert.Equal("Shift+B", KeybindFormatter.Format(new KeybindChord(0x42, KeybindFocusRules.ModifierShift)));
    }

    [Fact]
    public void SanitizeHoldChord_repairs_winui_shift_key16_corruption()
    {
        KeybindChord repaired = KeybindHoldRules.SanitizeHoldChord(
            new KeybindChord(KeybindHoldRules.GenericShift, KeybindFocusRules.ModifierShift));

        Assert.Equal(0xA0, repaired.Key);
        Assert.Equal(0, repaired.Modifiers);
        Assert.Equal("Shift", KeybindFormatter.Format(repaired));
    }
}
