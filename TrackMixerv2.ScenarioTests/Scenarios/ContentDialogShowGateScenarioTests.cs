using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class ContentDialogShowGateScenarioTests
{
    [Fact]
    public void TryAcquire_allows_only_one_active_dialog_slot()
    {
        ContentDialogShowGate.Release();

        Assert.True(ContentDialogShowGate.TryAcquire());
        Assert.True(ContentDialogShowGate.IsActive);
        Assert.False(ContentDialogShowGate.TryAcquire());

        ContentDialogShowGate.Release();
        Assert.False(ContentDialogShowGate.IsActive);
        Assert.True(ContentDialogShowGate.TryAcquire());
        ContentDialogShowGate.Release();
    }
}
