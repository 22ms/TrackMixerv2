using FlaUI.Core.AutomationElements;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UI")]
public sealed class RatingScenarioTests
{
    private readonly SharedUiAppFixture _fixture;

    public RatingScenarioTests(SharedUiAppFixture fixture) => _fixture = fixture;

    [Fact]
    public void User_can_change_clip_rating_from_the_slider()
    {
        var ratingSlider = UiWait.Until(
            () => _fixture.Session.FindByAutomationId("RatingSlider")?.AsSlider(),
            TimeSpan.FromSeconds(20),
            "rating slider");

        double initialValue = ratingSlider.Value;
        double targetValue = initialValue < 50 ? 75 : 25;
        ratingSlider.Value = targetValue;

        UiWait.UntilTrue(
            () => Math.Abs(ratingSlider.Value - targetValue) < 1,
            TimeSpan.FromSeconds(5),
            "rating slider value update");

        Assert.InRange(Math.Abs(ratingSlider.Value - targetValue), 0, 1);
    }
}
