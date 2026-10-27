using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class MediaDialogRulesScenarioTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    public void ShouldShowForOpenGeneration_skips_stale_media(
        int requestedGeneration,
        int currentGeneration,
        bool expected)
    {
        Assert.Equal(expected, MediaDialogRules.ShouldShowForOpenGeneration(requestedGeneration, currentGeneration));
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
