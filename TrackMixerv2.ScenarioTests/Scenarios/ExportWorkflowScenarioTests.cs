using System.Globalization;
using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class ExportWorkflowScenarioTests
{
    public ExportWorkflowScenarioTests()
    {
        AppState.Reset();
    }

    [Fact]
    public void Export_trim_range_affects_bitrate_budget()
    {
        double fullDuration = 120;
        var start = ExportPipeline.ParseTimeInput("30");
        var end = ExportPipeline.ParseTimeInput("90");

        double trimmedDuration = ExportPipeline.ComputeExportDurationSeconds(
            fullDuration,
            hasStartTime: true,
            start,
            hasEndTime: true,
            end);

        double fullBitrate = ExportPipeline.ComputeVideoBitrateKbps(25, fullDuration);
        double trimmedBitrate = ExportPipeline.ComputeVideoBitrateKbps(25, trimmedDuration);

        Assert.Equal(60, trimmedDuration);
        Assert.True(trimmedBitrate > fullBitrate);
    }

    [Fact]
    public void Export_mixes_all_audio_tracks_with_saved_slider_levels()
    {
        double[] sliderLevels = { 100, 50, 0 };
        double[] normalized = sliderLevels.Select(level => level / 100.0).ToArray();

        string? filter = ExportPipeline.BuildFilterComplex(3, normalized);

        Assert.Equal(
            "[0:a:0]volume=1[a0];[0:a:1]volume=0.5[a1];[0:a:2]volume=0[a2];[a0][a1][a2]amix=inputs=3:normalize=0[mixedaudio]",
            filter);
    }

    [Fact]
    public void Export_of_a_single_track_skips_amix()
    {
        Assert.Equal("[0:a:0]volume=0.8[mixedaudio]", ExportPipeline.BuildFilterComplex(1, new[] { 0.8 }));
    }

    [Fact]
    public void Export_of_a_video_without_audio_has_no_audio_filter()
    {
        Assert.Null(ExportPipeline.BuildFilterComplex(0, Array.Empty<double>()));
    }

    [Theory]
    [InlineData(@"C:\clips\a.mkv", ".mkv")]
    [InlineData(@"C:\clips\a.MP4", ".mp4")]
    [InlineData(@"C:\clips\a.webm", ".mp4")]
    [InlineData(@"C:\clips\a.avi", ".mp4")]
    [InlineData(@"C:\clips\a.wmv", ".mp4")]
    public void Export_container_can_hold_h264_and_aac(string input, string expected)
    {
        Assert.Equal(expected, ExportPipeline.GetOutputExtension(input));
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("1,5", 1.5)]
    [InlineData("01:02.5", 62.5)]
    [InlineData("01:02,5", 62.5)]
    public void Export_trim_parsing_does_not_depend_on_the_system_language(string input, double expectedSeconds)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "de-DE", "en-US" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ExportPipeline.ParseTimeInput(input));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Export_tolerates_fewer_sliders_than_audio_tracks()
    {
        string? filter = ExportPipeline.BuildFilterComplex(4, new[] { 0.75, 0.25 });

        Assert.Contains("[0:a:0]volume=0.75[a0]", filter);
        Assert.Contains("[0:a:1]volume=0.25[a1]", filter);
        Assert.Contains("[0:a:2]volume=1[a2]", filter);
        Assert.Contains("[0:a:3]volume=1[a3]", filter);
    }

    [Theory]
    [InlineData("45", 45)]
    [InlineData("01:30", 90)]
    [InlineData("00:01:05", 65)]
    public void Export_dialog_accepts_common_trim_formats(string input, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ExportPipeline.ParseTimeInput(input));
    }

    [Fact]
    public void Export_trim_with_end_before_start_yields_negative_duration()
    {
        var start = ExportPipeline.ParseTimeInput("90");
        var end = ExportPipeline.ParseTimeInput("30");

        double duration = ExportPipeline.ComputeExportDurationSeconds(
            fullDurationSeconds: 120,
            hasStartTime: true,
            start,
            hasEndTime: true,
            end);

        Assert.Equal(-60, duration);
    }

    [Fact]
    public void Export_trim_from_start_on_zero_length_source_is_zero()
    {
        var start = ExportPipeline.ParseTimeInput("15");

        double duration = ExportPipeline.ComputeExportDurationSeconds(
            fullDurationSeconds: 0,
            hasStartTime: true,
            start,
            hasEndTime: false,
            endTime: TimeSpan.Zero);

        Assert.Equal(0, duration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Export_rejects_blank_trim_input(string input)
    {
        Assert.Throws<FormatException>(() => ExportPipeline.ParseTimeInput(input));
    }

    [Fact]
    public void Export_rejects_garbled_trim_input()
    {
        Assert.Throws<FormatException>(() => ExportPipeline.ParseTimeInput("not-a-time"));
    }
}
