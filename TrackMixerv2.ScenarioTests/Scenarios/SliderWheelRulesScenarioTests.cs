using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class SliderWheelRulesScenarioTests
{
    [Theory]
    [InlineData(1, 5, 1, 1)]
    [InlineData(0, 5, 1, 5)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(0, 5, 2, 10)]
    [InlineData(1, 5, 2, 2)]
    public void GetWheelStep_prefers_small_change_then_tick_frequency_then_one_and_applies_speed(
        double smallChange,
        double tickFrequency,
        int speedMultiplier,
        double expected)
    {
        Assert.Equal(expected, SliderWheelRules.GetWheelStep(smallChange, tickFrequency, speedMultiplier));
    }
}
