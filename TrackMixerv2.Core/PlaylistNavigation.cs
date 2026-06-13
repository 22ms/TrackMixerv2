namespace TrackMixerv2;

public static class PlaylistNavigation
{
    public static string? FindExistingPathForward(PlaylistIndex playlist, int startIndex)
    {
        for (int i = startIndex; i < playlist.OrderedPaths.Count; i++)
        {
            string path = playlist.OrderedPaths[i];
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static string? FindExistingPathBackward(PlaylistIndex playlist, int startIndex)
    {
        for (int i = startIndex; i >= 0; i--)
        {
            string path = playlist.OrderedPaths[i];
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
