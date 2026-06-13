namespace TrackMixerv2.UITests.Infrastructure;

internal static class TrackMixerPaths
{
    public const string MultiTrackFixtureName = "uitest-clip.mp4";
    public const string SingleTrackFixtureName = "uitest-clip-single.mp4";

    public const int FixtureAudioTrackCount = 3;
    public const int SingleTrackFixtureAudioTrackCount = 1;

    private static readonly string[] RelativeExePaths =
    [
        Path.Combine("TrackMixerv2", "bin", "x64", "Debug", "net10.0-windows10.0.26100.0", "win-x64", "TrackMixerv2.exe"),
        Path.Combine("TrackMixerv2", "bin", "x64", "Release", "net10.0-windows10.0.26100.0", "win-x64", "TrackMixerv2.exe"),
        Path.Combine("TrackMixerv2", "bin", "x64", "Debug", "net10.0-windows10.0.26100.0", "win-x64", "publish", "TrackMixerv2.exe"),
        Path.Combine("TrackMixerv2", "bin", "x64", "Release", "net10.0-windows10.0.26100.0", "win-x64", "publish", "TrackMixerv2.exe"),
    ];

    public static string ResolveExePath()
    {
        string? directory = AppContext.BaseDirectory;
        for (int depth = 0; depth < 10 && directory != null; depth++)
        {
            foreach (string relativePath in RelativeExePaths)
            {
                string candidate = Path.Combine(directory, relativePath);
                if (File.Exists(candidate))
                    return candidate;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new FileNotFoundException(
            "TrackMixerv2.exe not found. Build the UI-test app first: scripts/run-uitests.ps1");
    }

    public static string ResolveFixtureClipPath(string fixtureName = MultiTrackFixtureName)
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        if (File.Exists(fixturePath))
            return fixturePath;

        throw new FileNotFoundException(
            $"UI test fixture clip not found at '{fixturePath}'. Run scripts/generate-uitest-fixture.ps1");
    }

    public static string CreateTempClipLibrary(string clipName = MultiTrackFixtureName)
    {
        string root = CreateTempLibraryRoot();
        string clipPath = Path.Combine(root, clipName);
        File.Copy(ResolveFixtureClipPath(clipName), clipPath, overwrite: true);
        return clipPath;
    }

    public static string CreateTempLibraryRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrackMixerUITests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    public static (string ClipA, string ClipB, string Root) CreateTwoClipLibrary()
    {
        string root = CreateTempLibraryRoot();
        string clipA = Path.Combine(root, "first-clip.mp4");
        string clipB = Path.Combine(root, "second-clip.mp4");
        string fixture = ResolveFixtureClipPath();
        File.Copy(fixture, clipA, overwrite: true);
        File.Copy(fixture, clipB, overwrite: true);
        return (clipA, clipB, root);
    }

    public static (string[] Clips, string Root) CreateChronoClipLibrary(
        IReadOnlyList<string> relativeNames,
        string clipName = MultiTrackFixtureName)
    {
        if (relativeNames.Count == 0)
            throw new ArgumentException("At least one clip name is required.", nameof(relativeNames));

        string root = CreateTempLibraryRoot();
        string fixture = ResolveFixtureClipPath(clipName);
        var clips = new string[relativeNames.Count];
        DateTime baseTime = DateTime.Now.AddHours(-relativeNames.Count);

        for (int i = 0; i < relativeNames.Count; i++)
        {
            clips[i] = Path.Combine(root, relativeNames[i]);
            File.Copy(fixture, clips[i], overwrite: true);
            File.SetCreationTime(clips[i], baseTime.AddMinutes(i));
        }

        return (clips, root);
    }

    public static (string MultiTrackClip, string SingleTrackClip, string Root) CreateMixedTrackCountLibrary()
    {
        string multiTrackClip = CreateTempClipLibrary();
        string root = Path.GetDirectoryName(multiTrackClip)!;
        string singleTrackClip = Path.Combine(root, SingleTrackFixtureName);
        File.Copy(ResolveFixtureClipPath(SingleTrackFixtureName), singleTrackClip, overwrite: true);
        File.SetCreationTime(multiTrackClip, DateTime.Now.AddMinutes(-1));
        File.SetCreationTime(singleTrackClip, DateTime.Now);
        return (multiTrackClip, singleTrackClip, root);
    }

    public static string CreateIsolatedTestHome()
    {
        string home = Path.Combine(Path.GetTempPath(), "TrackMixerUITests", "home-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        return home;
    }
}
