using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class SliderWheelRulesScenarioTests
{
    [Theory]
    [InlineData(1, 5, 1)]
    [InlineData(0, 5, 5)]
    [InlineData(0, 0, 1)]
    public void GetWheelStep_prefers_small_change_then_tick_frequency_then_one(
        double smallChange,
        double tickFrequency,
        double expected)
    {
        Assert.Equal(expected, SliderWheelRules.GetWheelStep(smallChange, tickFrequency));
    }
}
