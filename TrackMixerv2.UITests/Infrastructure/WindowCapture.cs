using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;

namespace TrackMixerv2.UITests.Infrastructure;

/// <summary>
/// Renders the app window itself rather than copying the screen, so other windows on top
/// of it (the developer keeps working while tests run) never end up in the image.
/// </summary>
public sealed class WindowCapture : IDisposable
{
    private const uint RenderFullContent = 2;
    private readonly nint _handle;
    private readonly Rectangle _windowBounds;

    public Bitmap Image { get; }

    public WindowCapture(Window window)
    {
        nint handle = _handle = window.Properties.NativeWindowHandle.Value;
        GetWindowRect(handle, out Rect rect);
        _windowBounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        Image = new Bitmap(_windowBounds.Width, _windowBounds.Height);
        using Graphics graphics = Graphics.FromImage(Image);
        nint dc = graphics.GetHdc();
        try
        {
            PrintWindow(handle, dc, RenderFullContent);
        }
        finally
        {
            graphics.ReleaseHdc(dc);
        }
    }

    /// <param name="screenBounds">Screen coordinates, as UIA reports them.</param>
    public Bitmap Crop(Rectangle screenBounds)
    {
        var local = new Rectangle(
            screenBounds.X - _windowBounds.X,
            screenBounds.Y - _windowBounds.Y,
            screenBounds.Width,
            screenBounds.Height);
        local.Intersect(new Rectangle(Point.Empty, Image.Size));
        return Image.Clone(local, Image.PixelFormat);
    }

    /// <summary>
    /// Minimize, maximize and close are drawn by the system and are not in the UIA tree of
    /// the content; they are three 46 x 32 px buttons in the top-right client corner.
    /// </summary>
    public Rectangle CaptionButtonsBounds()
    {
        GetClientRect(_handle, out Rect client);
        var origin = new Point();
        ClientToScreen(_handle, ref origin);
        double scale = GetDpiForWindow(_handle) / 96.0;
        int width = (int)Math.Round(3 * 46 * scale);
        int height = (int)Math.Round(32 * scale);
        return new Rectangle(origin.X + client.Right - width, origin.Y, width, height);
    }

    public void Dispose() => Image.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(nint hwnd, ref Point point);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(nint hwnd, nint hdc, uint flags);
}
