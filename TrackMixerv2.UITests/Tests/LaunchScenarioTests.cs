using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UI")]
public sealed class LaunchScenarioTests
{
    private readonly SharedUiAppFixture _fixture;

    public LaunchScenarioTests(SharedUiAppFixture fixture) => _fixture = fixture;

    [Fact]
    public void App_launches_without_file_picker_or_root_folder_dialog()
    {
        Assert.NotNull(_fixture.Session.MainWindow);
        Assert.True(_fixture.Session.MainWindow.BoundingRectangle.Width > 0);
    }

    [Fact]
    public void Launching_with_clip_opens_a_tab_for_that_file()
    {
        string clipName = Path.GetFileName(_fixture.ClipPath);
        UiWait.UntilTrue(
            () => _fixture.Session.FindByAutomationId("RatingSlider") != null
                || _fixture.Session.VideoTitleContains(clipName)
                || _fixture.Session.WindowTitleContains(clipName),
            TimeSpan.FromSeconds(20),
            "mixer page for launched clip");

        Assert.True(
            _fixture.Session.FindByAutomationId("RatingSlider") != null
            || _fixture.Session.VideoTitleContains(clipName)
            || _fixture.Session.WindowTitleContains(clipName));
    }
}
