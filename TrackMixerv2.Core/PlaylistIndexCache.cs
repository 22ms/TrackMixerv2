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

    // Indexes are built outside CacheLock: a scan of a large or sleeping drive must not block
    // the UI thread, which also takes the lock. Two concurrent builds just race to the same result.
    public static PlaylistIndex GetChrono(string rootFolder)
    {
        string key = ChronoKey(rootFolder);
        lock (CacheLock)
        {
            if (ChronoIndexes.TryGetValue(key, out PlaylistIndex? cached))
                return cached;
        }

        PlaylistIndex built = BuildChronoIndex(rootFolder);
        lock (CacheLock)
            ChronoIndexes[key] = built;
        return built;
    }

    /// <summary>Clips created within <paramref name="window"/> (e.g. the last 30 days), best rated first.</summary>
    public static PlaylistIndex GetRating(string rootFolder, TimeSpan window)
    {
        string key = RatingKey(rootFolder, window);
        lock (CacheLock)
        {
            if (RatingIndexes.TryGetValue(key, out PlaylistIndex? cached))
                return cached;
        }

        PlaylistIndex built = BuildRatingIndex(rootFolder, DateTime.Now - window);
        lock (CacheLock)
            RatingIndexes[key] = built;
        return built;
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

    private static string RatingKey(string rootFolder, TimeSpan window) =>
        NormalizeKey(rootFolder) + "|" + window.Ticks;

    private static string NormalizeKey(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static PlaylistIndex BuildChronoIndex(string rootFolder)
    {
        if (!Directory.Exists(rootFolder))
            return CreateIndex([]);

        // A root at a drive letter contains folders like "System Volume Information" that
        // throw UnauthorizedAccessException; skip those instead of failing the whole scan.
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
        };
        var ordered = Directory.EnumerateFiles(rootFolder, "*", options)
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
        List<(string path, double rating)> candidates;
        lock (AppState.TrackMetadataLock)
        {
            candidates = AppState.TRACK_METADATA
                .Where(pair => Helper.PathIsUnderDirectory(pair.Key, rootFolder))
                .Select(pair => (pair.Key, pair.Value.Rating))
                .ToList();
        }

        var matches = candidates
            .Where(entry => File.Exists(entry.path) && FileTimeCache.GetCreationTime(entry.path) > afterThis)
            .ToList();

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
