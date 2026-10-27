using System.Drawing;
using FlaUI.Core.Input;
using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

/// <summary>
/// Runs the app in both themes and checks that text stands out from what is behind it.
/// Screenshots land in theme-screenshots next to the test binaries for a visual check.
/// </summary>
[Collection(UiTestMutatingCollection.Name)]
[Trait("Category", "UI")]
public sealed class ThemeUiTests
{
    private const double MinimumContrast = 3.0;

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Text_is_readable_in_both_themes(string theme)
    {
        string clip = TrackMixerPaths.CreateTempClipLibrary();
        using var session = new TrackMixerAppSession(clip);
        session.SetTheme(theme);
        session.Launch();
        session.WaitForDiagnostics(d => d.Position > 1, "playback to start", seconds: 30);
        session.SetRangeValue("VolumeSlider_1", VolumeScale.ToPosition(160));
        session.WaitForDiagnostics(d => Math.Abs((d.Tracks[1].Volume ?? 0) - 1.6) < 0.01, "track 1 boosted, so the screenshot shows the boost color");

        string folder = Path.Combine(AppContext.BaseDirectory, "theme-screenshots");
        Directory.CreateDirectory(folder);
        using var capture = new WindowCapture(session.MainWindow);
        capture.Image.Save(Path.Combine(folder, $"{theme}-window.png"));

        var regions = new[] { "TimeElapsed", "TimeRemaining", "VideoTitle" }
            .Select(id => (id, session.Require(id).BoundingRectangle))
            .Append(("CaptionButtons", capture.CaptionButtonsBounds()));
        var failures = new List<string>();
        foreach (var (name, bounds) in regions)
        {
            using Bitmap image = capture.Crop(bounds);
            image.Save(Path.Combine(folder, $"{theme}-{name}.png"));
            double contrast = TextContrast(image);
            if (contrast < MinimumContrast)
                failures.Add($"{name}: {contrast:0.00}");
        }

        session.Require("TrackVolumeButton").Patterns.Invoke.Pattern.Invoke();
        UiWait.Until(() => session.FindByAutomationId("VolumeFlyoutSlider_1"), TimeSpan.FromSeconds(5), "the track volume flyout");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
        using (var flyout = new WindowCapture(session.MainWindow))
            flyout.Image.Save(Path.Combine(folder, $"{theme}-flyout.png"));

        Assert.True(failures.Count == 0, $"{theme} theme, low contrast: {string.Join(", ", failures)}");
    }

    /// <summary>
    /// The most common color is the background; the pixel furthest from it in contrast is
    /// the text. Returns their WCAG contrast ratio.
    /// </summary>
    private static double TextContrast(Bitmap bitmap)
    {
        var counts = new Dictionary<int, int>();
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                int argb = bitmap.GetPixel(x, y).ToArgb();
                counts[argb] = counts.GetValueOrDefault(argb) + 1;
            }

        double background = Luminance(Color.FromArgb(counts.MaxBy(pair => pair.Value).Key));
        return counts.Keys
            .Select(argb => Ratio(background, Luminance(Color.FromArgb(argb))))
            .Max();
    }

    private static double Ratio(double a, double b) =>
        (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
