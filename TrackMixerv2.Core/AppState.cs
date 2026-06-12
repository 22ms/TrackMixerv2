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

    public static List<string>? ROOT_FOLDERS;
    public static Dictionary<string, TrackMetadata> TRACK_METADATA = new();
    public static bool RootFolderPromptSuppressed;

    public static string? RootFoldersContainFile(string? path)
    {
        if (ROOT_FOLDERS == null || path == null)
            return null;

        foreach (var folder in ROOT_FOLDERS)
        {
            if (Helper.PathIsUnderDirectory(path, folder))
                return folder;
        }

        return null;
    }

    public static void Reset()
    {
        ROOT_FOLDERS = null;
        TRACK_METADATA = new Dictionary<string, TrackMetadata>();
        RootFolderPromptSuppressed = false;
        PlaylistHelper.EnsureRootFolderAsync = null;
    }
}
