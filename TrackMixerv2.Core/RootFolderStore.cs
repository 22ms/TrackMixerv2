namespace TrackMixerv2;

public static class RootFolderStore
{
    /// <summary>
    /// Scenario tests must disable this so temp folders are not written to the real user profile.
    /// </summary>
    public static bool PersistToUserEnvironment { get; set; } = true;

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
        RemoveKnownTestArtifactFolders(persistIfChanged: true);
    }

    public static bool IsKnownTestArtifactFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return false;

        string fullPath = Path.GetFullPath(folder);
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        string name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name.StartsWith("TrackMixerRoot", StringComparison.OrdinalIgnoreCase);
    }

    static void RemoveKnownTestArtifactFolders(bool persistIfChanged)
    {
        if (AppState.ROOT_FOLDERS == null || AppState.ROOT_FOLDERS.Count == 0)
            return;

        int before = AppState.ROOT_FOLDERS.Count;
        AppState.ROOT_FOLDERS.RemoveAll(IsKnownTestArtifactFolder);
        if (persistIfChanged && AppState.ROOT_FOLDERS.Count != before)
            Persist();
    }

    public static bool Add(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return false;

        EnsureLoaded();
        string normalized = Path.GetFullPath(folder);
        var folders = AppState.ROOT_FOLDERS!;
        if (folders.Any(existing =>
                string.Equals(Path.GetFullPath(existing), normalized, StringComparison.OrdinalIgnoreCase)))
            return false;

        folders.Add(normalized);
        Persist();
        PlaylistIndexCache.InvalidateChrono(normalized);
        return true;
    }

    public static bool Remove(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return false;

        EnsureLoaded();
        string normalized = Path.GetFullPath(folder);
        int index = AppState.ROOT_FOLDERS!.FindIndex(existing =>
            string.Equals(Path.GetFullPath(existing), normalized, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        string removed = AppState.ROOT_FOLDERS[index];
        AppState.ROOT_FOLDERS.RemoveAt(index);
        Persist();
        PlaylistIndexCache.InvalidateChrono(removed);
        return true;
    }

    public static void Persist()
    {
        if (!PersistToUserEnvironment)
            return;

        EnsureLoaded();
        if (AppState.ROOT_FOLDERS!.Count == 0)
        {
            Task.Run(() => Environment.SetEnvironmentVariable(
                AppPaths.RootFoldersEnvVar, null, EnvironmentVariableTarget.User));
            return;
        }

        string value = string.Join(';', AppState.ROOT_FOLDERS);
        Task.Run(() => Environment.SetEnvironmentVariable(
            AppPaths.RootFoldersEnvVar, value, EnvironmentVariableTarget.User));
    }
}
