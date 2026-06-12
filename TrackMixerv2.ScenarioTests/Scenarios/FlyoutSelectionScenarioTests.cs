namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class FlyoutSelectionScenarioTests
{
  [Theory]
  [InlineData("forward", "forward", true)]
  [InlineData("off", "forward", false)]
  [InlineData(null, "forward", false)]
  public void TagMatches_compares_ordinal(string? tag, string expected, bool matches)
  {
    Assert.Equal(matches, FlyoutSelection.TagMatches(tag, expected));
  }

  [Theory]
  [InlineData(1.0, 1.0, true)]
  [InlineData(1.25, 1.25, true)]
  [InlineData(1.0004, 1.0, true)]
  [InlineData(2.0, 1.0, false)]
  [InlineData("1", 1.0, false)]
  public void PlaybackRateMatches_compares_with_tolerance(object? tag, double rate, bool matches)
  {
    Assert.Equal(matches, FlyoutSelection.PlaybackRateMatches(tag, rate));
  }
}
