namespace TrackMixerv2;

/// <summary>
/// Global flag that suppresses every playback/navigation shortcut while the user is
/// recording a new keybind. This includes the speed-boost/slow-motion hold polling loop,
/// which reads physical key state directly and would otherwise fire mid-rebind.
/// </summary>
public static class KeybindRecordingGate
{
    private static volatile bool _isRecording;

    public static bool IsRecording => _isRecording;

    public static void Set(bool recording) => _isRecording = recording;
}
