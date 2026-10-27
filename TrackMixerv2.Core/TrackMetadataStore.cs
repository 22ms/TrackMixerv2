using System.Diagnostics;
using Newtonsoft.Json;

namespace TrackMixerv2;

public static class TrackMetadataStore
{
    private static readonly SemaphoreSlim PersistGate = new(1, 1);
    private static long _latestSnapshot;

    public static void UpdateEntry(Dictionary<string, TrackMetadata> metadata, string path, double rating, IReadOnlyList<double> sliders)
    {
        var slidersCopy = new List<double>(sliders);
        lock (AppState.TrackMetadataLock)
        {
            if (metadata.TryGetValue(path, out TrackMetadata? existing))
            {
                existing.Rating = rating;
                existing.Sliders = slidersCopy;
            }
            else
            {
                metadata.Add(path, new TrackMetadata(rating, slidersCopy));
            }
        }

        PlaylistIndexCache.InvalidateRating();
        FileTimeCache.Remove(path);
    }

    public static void RemoveEntry(Dictionary<string, TrackMetadata> metadata, string path)
    {
        lock (AppState.TrackMetadataLock)
            metadata.Remove(path);

        PlaylistIndexCache.InvalidateRating();
        FileTimeCache.Remove(path);
    }

    public static async Task PersistAsync(Dictionary<string, TrackMetadata> metadata, string jsonPath)
    {
        // Snapshot on the caller's thread so nothing can mutate the dictionary mid-serialize.
        string json;
        long snapshot;
        lock (AppState.TrackMetadataLock)
        {
            json = JsonConvert.SerializeObject(metadata);
            snapshot = ++_latestSnapshot;
        }

        await PersistGate.WaitAsync().ConfigureAwait(false);
        try
        {
            // A newer snapshot is queued behind this one; writing this would be wasted work.
            if (snapshot < Interlocked.Read(ref _latestSnapshot))
                return;

            await AtomicFile.WriteAllTextAsync(jsonPath, json).ConfigureAwait(false);
        }
        finally
        {
            PersistGate.Release();
        }
    }

    /// <summary>
    /// Never throws. An unreadable file is moved aside (see <see cref="AtomicFile.Quarantine"/>)
    /// rather than silently replaced by the next save.
    /// </summary>
    public static Dictionary<string, TrackMetadata> Load(string jsonPath)
    {
        var result = new Dictionary<string, TrackMetadata>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(jsonPath))
            return result;

        try
        {
            string json = File.ReadAllText(jsonPath);
            if (string.IsNullOrWhiteSpace(json))
                return result;

            var loaded = JsonConvert.DeserializeObject<Dictionary<string, TrackMetadata>>(json);
            if (loaded == null)
                return result;

            foreach (var (path, entry) in loaded)
            {
                if (entry == null)
                    continue;
                entry.Sliders ??= new List<double>();
                result.TryAdd(path, entry);
            }

            return result;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            try
            {
                Trace.WriteLine($"Track metadata unreadable, moved to {AtomicFile.Quarantine(jsonPath)}: {ex.Message}");
            }
            catch (IOException)
            {
            }

            return result;
        }
    }
}
