namespace TrackMixerv2;

public static class SeekMath
{
    /// <summary>
    /// Position after skipping <paramref name="offsetMillis"/>. Rewinding stops at zero; a skip
    /// past the end is ignored, so tapping forward near the end does nothing.
    /// </summary>
    public static TimeSpan AddOffset(TimeSpan position, int offsetMillis, TimeSpan naturalDuration)
    {
        TimeSpan newPosition = position + TimeSpan.FromMilliseconds(offsetMillis);

        if (newPosition < TimeSpan.Zero)
            return TimeSpan.Zero;

        if (naturalDuration != TimeSpan.Zero && newPosition >= naturalDuration)
            return position;

        return newPosition;
    }
}
