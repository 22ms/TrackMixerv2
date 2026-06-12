namespace TrackMixerv2;

public static class Helper
{
    public static readonly string[] VideoExtensions =
    {
        ".mp4",
        ".m4v",
        ".mkv",
        ".avi",
        ".wmv",
        ".mov",
        ".webm",
        ".mpg",
        ".mpeg",
        ".ts",
        ".m2ts"
    };

    public static string GetTitleFromPath(string path)
    {
        return Path.GetFileName(path);
    }

    public static bool PathIsUnderDirectory(string filePath, string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(directoryPath))
            return false;

        string normalizedFile = Path.GetFullPath(filePath);
        string normalizedDir = Path.GetFullPath(directoryPath);
        if (!normalizedDir.EndsWith(Path.DirectorySeparatorChar))
            normalizedDir += Path.DirectorySeparatorChar;

        return normalizedFile.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSupportedVideoPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return VideoExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
