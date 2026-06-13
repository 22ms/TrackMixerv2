namespace TrackMixerv2;

public enum HoldRecordingKeyDownResult
{
    ModifierPending,
    NonModifierPressed,
}

public sealed class KeybindHoldRecordingState
{
    public int? ModifierCandidateKey { get; private set; }
    public bool NonModifierPressed { get; private set; }

    public void Reset()
    {
        ModifierCandidateKey = null;
        NonModifierPressed = false;
    }

    public HoldRecordingKeyDownResult OnKeyDown(int virtualKey)
    {
        if (KeybindHoldRules.IsModifierVirtualKey(virtualKey))
        {
            ModifierCandidateKey = virtualKey;
            return HoldRecordingKeyDownResult.ModifierPending;
        }

        NonModifierPressed = true;
        ModifierCandidateKey = null;
        return HoldRecordingKeyDownResult.NonModifierPressed;
    }

    public KeybindChord? TryFinalizeModifierOnlyOnKeyUp(int virtualKey)
    {
        if (NonModifierPressed || ModifierCandidateKey == null)
            return null;

        if (!KeybindHoldRules.IsModifierVirtualKey(virtualKey))
            return null;

        if (!KeybindHoldRules.ModifierKeysMatch(ModifierCandidateKey.Value, virtualKey))
            return null;

        return KeybindHoldRules.CreateModifierOnlyChord(virtualKey);
    }
}
