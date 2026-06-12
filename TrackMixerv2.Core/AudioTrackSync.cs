namespace TrackMixerv2;

public static class AudioTrackSync
{
    public static readonly TimeSpan DefaultResyncThreshold = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan SyncCheckInterval = TimeSpan.FromSeconds(2);

    public static TimeSpan ComputeDrift(TimeSpan master, TimeSpan follower)
    {
        return (master - follower).Duration();
    }

    public static bool ShouldResync(TimeSpan master, TimeSpan follower, TimeSpan threshold)
    {
        return ComputeDrift(master, follower) > threshold;
    }

    public static bool ShouldResyncAny(TimeSpan master, IEnumerable<TimeSpan> followerPositions, TimeSpan threshold)
    {
        foreach (TimeSpan follower in followerPositions)
        {
            if (ShouldResync(master, follower, threshold))
                return true;
        }

        return false;
    }

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
