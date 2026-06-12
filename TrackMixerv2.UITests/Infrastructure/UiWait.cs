namespace TrackMixerv2.UITests.Infrastructure;

internal static class UiWait
{
    public static T Until<T>(Func<T?> probe, TimeSpan timeout, string description) where T : class
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        T? last = null;

        while (DateTime.UtcNow < deadline)
        {
            last = probe();
            if (last != null)
                return last;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    public static void UntilTrue(Func<bool> probe, TimeSpan timeout, string description)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (probe())
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
