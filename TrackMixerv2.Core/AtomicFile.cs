namespace TrackMixerv2;

/// <summary>
/// Writes go to a temp file that then replaces the target in one rename, so a crash or
/// power loss mid-save leaves the previous file intact instead of a truncated one.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string contents)
    {
        string temp = PrepareTemp(path);
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(contents);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    public static async Task WriteAllTextAsync(string path, string contents)
    {
        string temp = PrepareTemp(path);
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(contents).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Moves an unreadable file aside so the next save cannot overwrite the user's data.
    /// Returns the new path.
    /// </summary>
    public static string Quarantine(string path)
    {
        string target = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Move(path, target, overwrite: true);
        return target;
    }

    private static string PrepareTemp(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return path + ".tmp";
    }
}
