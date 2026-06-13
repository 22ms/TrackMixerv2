using System.Globalization;

namespace TrackMixerv2;

public static class DecimalInput
{
    public static bool TryParse(string? input, out double value)
    {
        value = double.NaN;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        if (TryParseWithCulture(input, CultureInfo.InvariantCulture, out value))
            return true;

        if (input.Contains('.') && !input.Contains(','))
            return TryParseWithCulture(input, CultureInfo.InvariantCulture, out value);

        if (TryParseWithCulture(input, CultureInfo.CurrentCulture, out value))
            return true;

        if (input.Contains(',') && !input.Contains('.'))
            return TryParseWithCulture(input.Replace(',', '.'), CultureInfo.InvariantCulture, out value);

        return false;
    }

    private static bool TryParseWithCulture(string input, CultureInfo culture, out double value) =>
        double.TryParse(input, NumberStyles.Float, culture, out value);
}
