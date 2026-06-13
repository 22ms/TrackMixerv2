namespace TrackMixerv2;

public static class ContentDialogShowGate
{
    private static int _activeShows;

    public static bool TryAcquire() => Interlocked.CompareExchange(ref _activeShows, 1, 0) == 0;

    public static void Release() => Interlocked.Exchange(ref _activeShows, 0);

    public static bool IsActive => Volatile.Read(ref _activeShows) != 0;
}
