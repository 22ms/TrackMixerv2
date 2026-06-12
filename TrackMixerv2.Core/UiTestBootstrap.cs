namespace TrackMixerv2;

public static class UiTestBootstrap
{
    public const string InstanceKey = "uitest";
    public const string EnabledEnvVar = "TRACKMIXER_UITEST";
    public const string LaunchFileEnvVar = "TRACKMIXER_LAUNCH_FILE";
    public const string RootFolderEnvVar = "TRACKMIXER_ROOT_FOLDER";
    public const string SuppressRootPromptEnvVar = "TRACKMIXER_SUPPRESS_ROOT_PROMPT";

    private static string? isolatedStorageRoot;

    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnabledEnvVar), "1", StringComparison.Ordinal);

    public static string? LaunchFile =>
        Environment.GetEnvironmentVariable(LaunchFileEnvVar);

    public static string? RootFolder =>
        Environment.GetEnvironmentVariable(RootFolderEnvVar);

    public static bool SuppressRootFolderPrompt =>
        IsEnabled ||
        string.Equals(Environment.GetEnvironmentVariable(SuppressRootPromptEnvVar), "1", StringComparison.Ordinal);

    public static IReadOnlyList<string> ResolveRootFoldersFromEnvironment()
    {
        if (!string.IsNullOrWhiteSpace(RootFolder))
            return new[] { RootFolder };

        string? configured = Environment.GetEnvironmentVariable(AppPaths.RootFoldersEnvVar);
        if (string.IsNullOrWhiteSpace(configured))
            return Array.Empty<string>();

        return configured
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    public static void ResetIsolatedStorageForTests() => isolatedStorageRoot = null;

    public static string GetIsolatedStorageDirectory()
    {
        if (isolatedStorageRoot == null)
        {
            isolatedStorageRoot = Path.Combine(
                Path.GetTempPath(),
                "TrackMixerUITests",
                "pid-" + Environment.ProcessId);
            Directory.CreateDirectory(isolatedStorageRoot);
        }

        return isolatedStorageRoot;
    }
}
