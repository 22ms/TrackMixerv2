using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrackMixerv2.UITests.Infrastructure;

internal static class DpiAwareness
{
    private static readonly nint PerMonitorAwareV2 = -4;

    // testhost.exe is DPI-unaware, so on a scaled display UIA rectangles and SendInput
    // coordinates disagree and clicks land closer to the screen origin than intended.
    [ModuleInitializer]
    internal static void Enable() => SetProcessDpiAwarenessContext(PerMonitorAwareV2);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(nint value);
}
