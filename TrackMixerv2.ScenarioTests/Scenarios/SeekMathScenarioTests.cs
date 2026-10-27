using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class SeekMathScenarioTests
{
    [Fact]
    public void AddOffset_advances_position_within_duration()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(12.5),
            SeekMath.AddOffset(TimeSpan.FromSeconds(10), 2500, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void AddOffset_rewinds_and_clamps_to_zero()
    {
        Assert.Equal(TimeSpan.Zero, SeekMath.AddOffset(TimeSpan.FromSeconds(1), -5000, TimeSpan.FromSeconds(60)));
    }

    [Theory]
    [InlineData(58)]
    [InlineData(55)]
    public void AddOffset_does_not_seek_to_or_past_the_end(double seconds)
    {
        var position = TimeSpan.FromSeconds(seconds);
        Assert.Equal(position, SeekMath.AddOffset(position, 5000, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void AddOffset_allows_seek_when_duration_is_unknown()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(63),
            SeekMath.AddOffset(TimeSpan.FromSeconds(58), 5000, TimeSpan.Zero));
    }
}
