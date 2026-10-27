namespace TrackMixerv2;

/// <summary>
/// Maps a volume slider's position to a volume in percent. 0 to 100% fills the first three
/// quarters of the slider and the boost up to 200% the last quarter, so everyday adjustments
/// get most of the width. One position step is 1% below 100 and 3% above.
/// </summary>
public static class VolumeScale
{
    public const double MaxVolume = 200;
    private const double BoostRatio = 3;

    public static double MaxPosition { get; } = 100 + (MaxVolume - 100) / BoostRatio;

    public static double ToVolume(double position) =>
        position <= 100 ? position : 100 + (position - 100) * BoostRatio;

    public static double ToPosition(double volume)
    {
        volume = Math.Clamp(volume, 0, MaxVolume);
        return volume <= 100 ? volume : 100 + (volume - 100) / BoostRatio;
    }

    public static string Format(double volume) => $"{Math.Round(volume)}%";
}
