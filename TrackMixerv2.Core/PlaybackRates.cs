namespace TrackMixerv2;

public static class PlaybackRates
{
    public static readonly IReadOnlyList<double> Defaults =
    [
        0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3, 4,
    ];

    public const double MaxTransportRate = 16;

    public static bool IsValidTransportRate(double rate) =>
        rate > 0 && rate <= MaxTransportRate;

    public static IReadOnlyList<double> All => LocalSettingsStore.GetTransportRates();

    public static IEnumerable<double> BoostRates => All.Where(rate => rate > 1);

    public static IEnumerable<double> SlowRates => All.Where(rate => rate < 1);

    public static double NormalizeBoostRate(double rate) =>
        SnapToNearest(rate, BoostRates, LocalSettingsStore.DefaultSpeedBoostRate);

    public static double NormalizeSlowRate(double rate) =>
        SnapToNearest(rate, SlowRates, LocalSettingsStore.DefaultSpeedSlowRate);

    public static double SnapToNearestTransportRate(double rate) =>
        SnapToNearest(rate, All, 1);

    public static IReadOnlyList<double> SanitizeTransportRates(IEnumerable<double>? rates)
    {
        if (rates == null)
            return Defaults.ToArray();

        var sanitized = rates
            .Where(rate => IsValidTransportRate(rate))
            .Select(rate => Math.Round(rate, 2))
            .Distinct()
            .OrderBy(rate => rate)
            .ToList();

        if (!sanitized.Any(rate => Math.Abs(rate - 1) < 0.001))
            sanitized.Add(1);

        sanitized.Sort();

        return sanitized.Count == 0 ? Defaults.ToArray() : sanitized;
    }

    public static IEnumerable<double> GetBoostRatesFrom(IEnumerable<double> rates) =>
        rates.Where(rate => rate > 1);

    public static IEnumerable<double> GetSlowRatesFrom(IEnumerable<double> rates) =>
        rates.Where(rate => rate < 1);

    public static double SnapBoostFromRates(double rate, IEnumerable<double> transportRates) =>
        SnapToNearest(rate, GetBoostRatesFrom(transportRates), LocalSettingsStore.DefaultSpeedBoostRate);

    public static double SnapSlowFromRates(double rate, IEnumerable<double> transportRates) =>
        SnapToNearest(rate, GetSlowRatesFrom(transportRates), LocalSettingsStore.DefaultSpeedSlowRate);

    public static string Format(double rate)
    {
        if (Math.Abs(rate - 1) < 0.001)
            return "1×";

        string text = rate % 1 == 0
            ? rate.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return text + "×";
    }

    private static double SnapToNearest(double rate, IEnumerable<double> allowed, double fallback)
    {
        double best = fallback;
        double bestDistance = double.MaxValue;
        bool found = false;

        foreach (double candidate in allowed)
        {
            found = true;
            double distance = Math.Abs(candidate - rate);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate;
        }

        return found ? best : fallback;
    }
}
