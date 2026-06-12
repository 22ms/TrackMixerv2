using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestMutatingCollection.Name)]
[Trait("Category", "UI")]
public sealed class TabScenarioTests
{
    [Fact]
    public void User_closes_tab_with_ctrl_w_shortcut()
    {
        string clipPath = TrackMixerPaths.CreateTempClipLibrary();
        using var session = new TrackMixerAppSession(clipPath);
        session.Launch();

        string clipName = Path.GetFileName(clipPath);
        UiWait.UntilTrue(
            () => session.FindByAutomationId("RatingSlider") != null,
            TimeSpan.FromSeconds(20),
            "mixer page to load");

        var tabView = session.FindByAutomationId("MainTabView");
        Assert.NotNull(tabView);
        tabView!.Focus();
        session.MainWindow.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);

        UiWait.UntilTrue(
            () => session.FindByAutomationId("RatingSlider") == null,
            TimeSpan.FromSeconds(10),
            "tab to close after Ctrl+W");

        Assert.Null(session.FindByAutomationId("RatingSlider"));
    }
}
