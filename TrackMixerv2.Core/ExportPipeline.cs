using System.Globalization;

namespace TrackMixerv2;

public static class ExportPipeline
{
    public static TimeSpan ParseTimeInput(string input)
    {
        input = input.Trim();

        if (string.IsNullOrEmpty(input))
            throw new FormatException("Time input is empty");

        if (!input.Contains(':') && DecimalInput.TryParse(input, out double seconds) && seconds >= 0)
            return TimeSpan.FromSeconds(seconds);

        if (input.Count(c => c == ':') == 1)
            input = "00:" + input;

        if (TimeSpan.TryParse(input.Replace(',', '.'), CultureInfo.InvariantCulture, out TimeSpan result) && result >= TimeSpan.Zero)
            return result;

        throw new FormatException("Invalid time format. Use SS, MM:SS, or HH:MM:SS.");
    }

    public static double ComputeExportDurationSeconds(
        double fullDurationSeconds,
        bool hasStartTime,
        TimeSpan startTime,
        bool hasEndTime,
        TimeSpan endTime)
    {
        if (hasStartTime && hasEndTime)
            return (endTime - startTime).TotalSeconds;

        if (hasEndTime)
            return endTime.TotalSeconds;

        if (hasStartTime)
            return Math.Max(0, fullDurationSeconds - startTime.TotalSeconds);

        return fullDurationSeconds;
    }

    public const string MixedAudioLabel = "[mixedaudio]";

    /// <summary>
    /// What the preview's PeakLimiter does to a track above 100%: the same ceiling and release,
    /// with the shortest attack alimiter allows. level=0 stops alimiter from raising the output.
    /// </summary>
    public const string BoostLimiterFilter = "alimiter=limit=0.98:attack=0.1:release=50:level=0";

    /// <summary>
    /// Applies each track's slider level and mixes them into <see cref="MixedAudioLabel"/>.
    /// Returns null when there is no audio to mix.
    /// </summary>
    public static string? BuildFilterComplex(int audioStreamCount, double[] normalizedLevels)
    {
        if (audioStreamCount <= 0)
            return null;

        var filterParts = new List<string>();
        for (int i = 0; i < audioStreamCount; i++)
        {
            double level = i < normalizedLevels.Length ? normalizedLevels[i] : 1.0;
            string output = audioStreamCount == 1 ? MixedAudioLabel : $"[a{i}]";
            string limiter = level > 1 ? "," + BoostLimiterFilter : "";
            filterParts.Add($"[0:a:{i}]volume={level.ToString("0.##", CultureInfo.InvariantCulture)}{limiter}{output}");
        }

        if (audioStreamCount > 1)
        {
            // amix divides every input by the input count by default, so a 3-track export came
            // out a third as loud as the preview, which sums the tracks.
            string audioInputs = string.Concat(Enumerable.Range(0, audioStreamCount).Select(i => $"[a{i}]"));
            filterParts.Add($"{audioInputs}amix=inputs={audioStreamCount}:normalize=0{MixedAudioLabel}");
        }

        return string.Join(";", filterParts);
    }

    private static readonly string[] H264AacContainers = [".mp4", ".m4v", ".mkv", ".mov", ".ts", ".m2ts"];

    /// <summary>
    /// The export re-encodes to H.264 + AAC, which .webm, .avi, .wmv and MPEG-PS cannot hold,
    /// so those export as .mp4.
    /// </summary>
    public static string GetOutputExtension(string inputPath)
    {
        string extension = Path.GetExtension(inputPath).ToLowerInvariant();
        return H264AacContainers.Contains(extension) ? extension : ".mp4";
    }

    public static double ComputeVideoBitrateKbps(double targetFileSizeMB, double durationSeconds, double audioBitrateBps = 128000)
    {
        double totalTargetBitrate = (targetFileSizeMB * 8 * 1024 * 1024) / durationSeconds;
        double videoBitrate = totalTargetBitrate - audioBitrateBps;
        return videoBitrate / 1000;
    }
}
