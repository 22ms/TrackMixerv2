namespace TrackMixerv2;

public static class KeybindHoldRules
{
    public const int GenericShift = 0x10;
    public const int GenericControl = 0x11;
    public const int GenericMenu = 0x12;

    public static bool IsHoldAction(KeybindAction action) =>
        action is KeybindAction.SpeedBoost or KeybindAction.SpeedSlow;

    public static bool IsModifierVirtualKey(int virtualKey) =>
        virtualKey is GenericShift or GenericControl or GenericMenu
            or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    public static bool ModifierKeysMatch(int firstKey, int secondKey) =>
        IsModifierVirtualKey(firstKey)
        && IsModifierVirtualKey(secondKey)
        && NormalizeModifierKey(firstKey) == NormalizeModifierKey(secondKey);

    public static KeybindChord CreateModifierOnlyChord(int virtualKey) =>
        new(NormalizeModifierKey(virtualKey), 0);

    public static int NormalizeModifierKey(int virtualKey) =>
        virtualKey switch
        {
            GenericShift or 0xA0 or 0xA1 => 0xA0,
            GenericControl or 0xA2 or 0xA3 => 0xA2,
            GenericMenu or 0xA4 or 0xA5 => 0xA4,
            _ => virtualKey,
        };

    public static bool IsModifierOnlyChord(KeybindChord chord) =>
        IsModifierVirtualKey(chord.Key) && chord.Modifiers == 0;

    public static KeybindChord SanitizeHoldChord(KeybindChord chord)
    {
        if (!IsModifierVirtualKey(chord.Key))
            return chord;

        int normalized = NormalizeModifierKey(chord.Key);
        int modifierBit = normalized switch
        {
            0xA0 => KeybindFocusRules.ModifierShift,
            0xA2 => KeybindFocusRules.ModifierControl,
            0xA4 => KeybindFocusRules.ModifierAlt,
            _ => 0,
        };

        if (chord.Modifiers == 0 || chord.Modifiers == modifierBit)
            return new KeybindChord(normalized, 0);

        return chord;
    }

    public static bool IsChordActive(
        KeybindChord chord,
        Func<int, bool> isKeyDown,
        Func<int> getModifiers)
    {
        if (IsModifierOnlyChord(chord))
            return IsEitherSideModifierDown(NormalizeModifierKey(chord.Key), isKeyDown);

        if (!isKeyDown(chord.Key))
            return false;

        return getModifiers() == chord.Modifiers;
    }

    private static bool IsEitherSideModifierDown(int normalizedKey, Func<int, bool> isKeyDown) =>
        normalizedKey switch
        {
            0xA0 => isKeyDown(0xA0) || isKeyDown(0xA1) || isKeyDown(GenericShift),
            0xA2 => isKeyDown(0xA2) || isKeyDown(0xA3) || isKeyDown(GenericControl),
            0xA4 => isKeyDown(0xA4) || isKeyDown(0xA5) || isKeyDown(GenericMenu),
            _ => isKeyDown(normalizedKey),
        };
}
