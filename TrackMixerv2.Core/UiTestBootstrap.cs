namespace TrackMixerv2;

public static class UiTestBootstrap
{
    public const string InstanceKey = "uitest";
    public const string EnabledEnvVar = "TRACKMIXER_UITEST";
    public const string LaunchFileEnvVar = "TRACKMIXER_LAUNCH_FILE";
    public const string RootFolderEnvVar = "TRACKMIXER_ROOT_FOLDER";
    public const string SuppressRootPromptEnvVar = "TRACKMIXER_SUPPRESS_ROOT_PROMPT";

    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnabledEnvVar), "1", StringComparison.Ordinal);

    public static string? LaunchFile =>
        Environment.GetEnvironmentVariable(LaunchFileEnvVar);

    public static string? RootFolder =>
        Environment.GetEnvironmentVariable(RootFolderEnvVar);

    public static bool SuppressRootFolderPrompt =>
        IsEnabled ||
        string.Equals(Environment.GetEnvironmentVariable(SuppressRootPromptEnvVar), "1", StringComparison.Ordinal);
}
