namespace TrackMixerv2;

public static class AppPaths
{
    public const string RootFoldersEnvVar = "TRACKMIXER_ROOT_FOLDERS";

    public static string DataDirectory =>
        UiTestBootstrap.IsEnabled
            ? UiTestBootstrap.GetIsolatedStorageDirectory()
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TrackMixerv2");

    public static string ResolveDataFilePath(string? overridePath, string fileName) =>
        !string.IsNullOrWhiteSpace(overridePath)
            ? overridePath
            : Path.Combine(DataDirectory, fileName);

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);

    public static string ScratchDirectory =>
        UiTestBootstrap.IsEnabled
            ? Path.Combine(UiTestBootstrap.GetIsolatedStorageDirectory(), "scratch")
            : Path.GetTempPath();

    public static string TempFilesRecordPath =>
        Path.Combine(
            UiTestBootstrap.IsEnabled
                ? UiTestBootstrap.GetIsolatedStorageDirectory()
                : Path.GetTempPath(),
            "TrackMixerTempFiles.txt");

    public static string UiTestCrashLogPath =>
        Path.Combine(UiTestBootstrap.GetIsolatedStorageDirectory(), "uitest-crash.txt");
}
