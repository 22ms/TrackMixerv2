namespace TrackMixerv2;

public static class MediaDialogRules
{
    public static bool ShouldShowForOpenGeneration(int requestedGeneration, int currentGeneration, bool isDisposing) =>
        !isDisposing && requestedGeneration == currentGeneration;

    public static bool ShouldShowCodecFailureDialog(string? mediaPath, bool mediaFileExists) =>
        !string.IsNullOrWhiteSpace(mediaPath) && mediaFileExists;
}
