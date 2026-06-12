using System.Globalization;

namespace TrackMixerv2;

public static class ExportPipeline
{
    public static TimeSpan ParseTimeInput(string input)
    {
        input = input.Trim();

        if (string.IsNullOrEmpty(input))
            throw new FormatException("Time input is empty");

        if (double.TryParse(input, out double seconds))
            return TimeSpan.FromSeconds(seconds);

        if (input.Count(c => c == ':') == 1)
            input = "00:" + input;

        if (TimeSpan.TryParse(input, out TimeSpan result))
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

    public static string BuildFilterComplex(int audioStreamCount, double[] normalizedLevels)
    {
        var filterParts = new List<string>();
        for (int i = 0; i < audioStreamCount; i++)
        {
            double level = i < normalizedLevels.Length ? normalizedLevels[i] : 1.0;
            filterParts.Add($"[0:a:{i}]volume={level.ToString("0.##", CultureInfo.InvariantCulture)}[a{i}]");
        }

        string filterComplex = string.Join(";", filterParts);
        string audioInputs = string.Join("", Enumerable.Range(0, audioStreamCount).Select(i => $"[a{i}]"));
        return filterComplex + $";{audioInputs}amix=inputs={audioStreamCount}[mixedaudio]";
    }

    public static double ComputeVideoBitrateKbps(double targetFileSizeMB, double durationSeconds, double audioBitrateBps = 128000)
    {
        double totalTargetBitrate = (targetFileSizeMB * 8 * 1024 * 1024) / durationSeconds;
        double videoBitrate = totalTargetBitrate - audioBitrateBps;
        return videoBitrate / 1000;
    }
}
