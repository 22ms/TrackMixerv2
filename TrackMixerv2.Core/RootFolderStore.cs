namespace TrackMixerv2;

public static class RootFolderStore
{
    private static readonly object PersistLock = new();
    private static Task _persistChain = Task.CompletedTask;

    /// <summary>
    /// Writing the User scope broadcasts WM_SETTINGCHANGE and can block for seconds, so it runs
    /// in the background, one write after another so the last change always wins. Tests point
    /// this at Process scope; UI-test runs never persist.
    /// </summary>
    public static EnvironmentVariableTarget PersistTarget { get; set; } = EnvironmentVariableTarget.User;

    public static IReadOnlyList<string> Folders
    {
        get
        {
            EnsureLoaded();
            return AppState.ROOT_FOLDERS!;
        }
    }

    public static void EnsureLoaded()
    {
        if (AppState.ROOT_FOLDERS != null)
            return;

        AppState.ROOT_FOLDERS = UiTestBootstrap.ResolveRootFoldersFromEnvironment().ToList();
    }

    public static bool Add(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return false;

        EnsureLoaded();
        string normalized = Normalize(folder);
        IReadOnlyList<string> folders = AppState.ROOT_FOLDERS!;
        if (folders.Any(existing => PathsEqual(existing, normalized)))
            return false;

        AppState.ROOT_FOLDERS = [.. folders, normalized];
        Persist();
        PlaylistIndexCache.InvalidateChrono(normalized);
        return true;
    }

    public static bool Remove(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return false;

        EnsureLoaded();
        string normalized = Normalize(folder);
        IReadOnlyList<string> folders = AppState.ROOT_FOLDERS!;
        string? removed = folders.FirstOrDefault(existing => PathsEqual(existing, normalized));
        if (removed == null)
            return false;

        AppState.ROOT_FOLDERS = folders.Where(existing => !ReferenceEquals(existing, removed)).ToList();
        Persist();
        PlaylistIndexCache.InvalidateChrono(removed);
        return true;
    }

    public static Task FlushAsync()
    {
        lock (PersistLock)
            return _persistChain;
    }

    private static void Persist()
    {
        if (UiTestBootstrap.IsEnabled)
            return;

        IReadOnlyList<string> folders = AppState.ROOT_FOLDERS!;
        string? value = folders.Count == 0 ? null : string.Join(';', folders);
        EnvironmentVariableTarget target = PersistTarget;
        lock (PersistLock)
        {
            _persistChain = _persistChain.ContinueWith(
                _ => Environment.SetEnvironmentVariable(AppPaths.RootFoldersEnvVar, value, target),
                TaskScheduler.Default);
        }
    }

    private static string Normalize(string folder) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    private static bool PathsEqual(string a, string normalizedB) =>
        string.Equals(Normalize(a), normalizedB, StringComparison.OrdinalIgnoreCase);
}
