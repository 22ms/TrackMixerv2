namespace TrackMixerv2;

public static class FileTimeCache
{
    private static readonly Dictionary<string, DateTime> CreationTimes = new(StringComparer.OrdinalIgnoreCase);

    public static DateTime GetCreationTime(string path)
    {
        lock (CreationTimes)
        {
            if (CreationTimes.TryGetValue(path, out DateTime cached))
                return cached;

            DateTime creationTime = File.GetCreationTime(path);
            CreationTimes[path] = creationTime;
            return creationTime;
        }
    }

    public static void Remove(string path)
    {
        lock (CreationTimes)
            CreationTimes.Remove(path);
    }

    public static void Clear()
    {
        lock (CreationTimes)
            CreationTimes.Clear();
    }
}
