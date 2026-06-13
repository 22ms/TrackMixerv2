namespace TrackMixerv2;

public sealed class PlaylistIndex
{
    public required IReadOnlyList<string> OrderedPaths { get; init; }
    public required Dictionary<string, int> PathToIndex { get; init; }

    public bool TryGetIndex(string path, out int index) =>
        PathToIndex.TryGetValue(path, out index);
}

public static class PlaylistIndexCache
{
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, PlaylistIndex> ChronoIndexes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, PlaylistIndex> RatingIndexes = new(StringComparer.OrdinalIgnoreCase);

    public static void Clear()
    {
        lock (CacheLock)
        {
            ChronoIndexes.Clear();
            RatingIndexes.Clear();
        }

        FileTimeCache.Clear();
    }

    public static void InvalidateChrono(string rootFolder)
    {
        lock (CacheLock)
            ChronoIndexes.Remove(NormalizeKey(rootFolder));
    }

    public static void InvalidateRating()
    {
        lock (CacheLock)
            RatingIndexes.Clear();
    }

    public static void NotifyMediaOpened(string filePath, bool subfolderOnly)
    {
        string? rootFolder = subfolderOnly
            ? Path.GetDirectoryName(filePath)
            : AppState.RootFoldersContainFile(filePath);

        if (rootFolder == null)
            return;

        string key = ChronoKey(rootFolder);
        lock (CacheLock)
        {
            if (!ChronoIndexes.TryGetValue(key, out PlaylistIndex? index))
                return;

            if (!index.PathToIndex.ContainsKey(filePath))
                ChronoIndexes.Remove(key);
        }
    }

    public static void NotifyMediaDeleted(string filePath, bool subfolderOnly)
    {
        string? rootFolder = subfolderOnly
            ? Path.GetDirectoryName(filePath)
            : AppState.RootFoldersContainFile(filePath);

        if (rootFolder != null)
            InvalidateChrono(rootFolder);

        InvalidateRating();
        FileTimeCache.Remove(filePath);
    }

    public static PlaylistIndex GetChrono(string rootFolder)
    {
        string key = ChronoKey(rootFolder);
        lock (CacheLock)
        {
            if (ChronoIndexes.TryGetValue(key, out PlaylistIndex? cached))
                return cached;

            cached = BuildChronoIndex(rootFolder);
            ChronoIndexes[key] = cached;
            return cached;
        }
    }

    public static PlaylistIndex GetRating(string rootFolder, DateTime afterThis)
    {
        string key = RatingKey(rootFolder, afterThis);
        lock (CacheLock)
        {
            if (RatingIndexes.TryGetValue(key, out PlaylistIndex? cached))
                return cached;

            cached = BuildRatingIndex(rootFolder, afterThis);
            RatingIndexes[key] = cached;
            return cached;
        }
    }

    public static PlaylistIndex GetChronoOrRebuild(string rootFolder, string currentFile)
    {
        if (!string.IsNullOrWhiteSpace(currentFile) && !File.Exists(currentFile))
        {
            InvalidateChrono(rootFolder);
            return GetChrono(rootFolder);
        }

        PlaylistIndex index = GetChrono(rootFolder);
        if (index.TryGetIndex(currentFile, out _))
            return index;

        if (!File.Exists(currentFile) || !Helper.PathIsUnderDirectory(currentFile, rootFolder))
            return index;

        InvalidateChrono(rootFolder);
        return GetChrono(rootFolder);
    }

    private static string ChronoKey(string rootFolder) => NormalizeKey(rootFolder);

    private static string RatingKey(string rootFolder, DateTime afterThis) =>
        NormalizeKey(rootFolder) + "|" + afterThis.Ticks;

    private static string NormalizeKey(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static PlaylistIndex BuildChronoIndex(string rootFolder)
    {
        var ordered = Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
            .Where(Helper.IsSupportedVideoPath)
            .Where(File.Exists)
            .Select(path => (path, created: FileTimeCache.GetCreationTime(path)))
            .OrderBy(entry => entry.created)
            .ThenBy(entry => entry.path, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.path)
            .ToList();

        return CreateIndex(ordered);
    }

    private static PlaylistIndex BuildRatingIndex(string rootFolder, DateTime afterThis)
    {
        List<(string path, double rating)> matches = new();

        lock (AppState.TrackMetadataLock)
        {
            foreach (KeyValuePair<string, TrackMetadata> pair in AppState.TRACK_METADATA)
            {
                if (!Helper.PathIsUnderDirectory(pair.Key, rootFolder))
                    continue;

                if (!File.Exists(pair.Key))
                    continue;

                if (FileTimeCache.GetCreationTime(pair.Key) <= afterThis)
                    continue;

                matches.Add((pair.Key, pair.Value.Rating));
            }
        }

        List<string> ordered = matches
            .OrderByDescending(entry => entry.rating)
            .ThenBy(entry => entry.path, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.path)
            .ToList();

        return CreateIndex(ordered);
    }

    private static PlaylistIndex CreateIndex(List<string> orderedPaths)
    {
        var pathToIndex = new Dictionary<string, int>(orderedPaths.Count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < orderedPaths.Count; i++)
            pathToIndex[orderedPaths[i]] = i;

        return new PlaylistIndex
        {
            OrderedPaths = orderedPaths,
            PathToIndex = pathToIndex
        };
    }
}
