using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class MediaDialogRulesScenarioTests
{
    [Theory]
    [InlineData(2, 2, false, true)]
    [InlineData(2, 3, false, false)]
    [InlineData(2, 2, true, false)]
    public void ShouldShowForOpenGeneration_skips_stale_or_disposing_media(
        int requestedGeneration,
        int currentGeneration,
        bool isDisposing,
        bool expected)
    {
        Assert.Equal(
            expected,
            MediaDialogRules.ShouldShowForOpenGeneration(requestedGeneration, currentGeneration, isDisposing));
    }

    [Theory]
    [InlineData("C:\\clip.mp4", true, true)]
    [InlineData("C:\\clip.mp4", false, false)]
    [InlineData(null, true, false)]
    public void ShouldShowCodecFailureDialog_requires_existing_media_path(
        string? mediaPath,
        bool mediaFileExists,
        bool expected)
    {
        Assert.Equal(expected, MediaDialogRules.ShouldShowCodecFailureDialog(mediaPath, mediaFileExists));
    }
}
