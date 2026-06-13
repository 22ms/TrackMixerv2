using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class KeybindRecordingGateScenarioTests : IDisposable
{
    public KeybindRecordingGateScenarioTests() => KeybindRecordingGate.Set(false);

    [Fact]
    public void Gate_is_inactive_by_default()
    {
        Assert.False(KeybindRecordingGate.IsRecording);
    }

    [Fact]
    public void Gate_reflects_recording_lifecycle()
    {
        KeybindRecordingGate.Set(true);
        Assert.True(KeybindRecordingGate.IsRecording);

        KeybindRecordingGate.Set(false);
        Assert.False(KeybindRecordingGate.IsRecording);
    }

    public void Dispose() => KeybindRecordingGate.Set(false);
}
