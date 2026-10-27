namespace TrackMixerv2;

public class TrackMetadata
{
    public double Rating;
    public List<double> Sliders;

    public TrackMetadata(double rating, List<double> sliders)
    {
        Rating = rating;
        Sliders = sliders;
    }
}

public static class AppState
{
    public static readonly object TrackMetadataLock = new object();
    public const string TrackMetadataJsonEnvVar = "TRACKMIXER_METADATA_PATH";

    public static string TrackMetadataJson =>
        AppPaths.ResolveDataFilePath(
            Environment.GetEnvironmentVariable(TrackMetadataJsonEnvVar),
            "track_metadata.json");

    /// <summary>
    /// Replaced, never mutated in place: the playlist prewarm thread iterates it while the
    /// UI thread adds or removes folders.
    /// </summary>
    public static IReadOnlyList<string>? ROOT_FOLDERS;
    public static Dictionary<string, TrackMetadata> TRACK_METADATA = new(StringComparer.OrdinalIgnoreCase);
    public static bool RootFolderPromptSuppressed;

    public static string? RootFoldersContainFile(string? path)
    {
        IReadOnlyList<string>? folders = ROOT_FOLDERS;
        if (folders == null || path == null)
            return null;

        foreach (var folder in folders)
        {
            if (Helper.PathIsUnderDirectory(path, folder))
                return folder;
        }

        return null;
    }

    public static void Reset()
    {
        ROOT_FOLDERS = null;
        TRACK_METADATA = new Dictionary<string, TrackMetadata>(StringComparer.OrdinalIgnoreCase);
        RootFolderPromptSuppressed = false;
        PlaylistHelper.EnsureRootFolderAsync = null;
        PlaylistIndexCache.Clear();
    }
}
