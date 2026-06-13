using Newtonsoft.Json;

namespace TrackMixerv2;

public static class TrackMetadataStore
{
    private static readonly SemaphoreSlim PersistGate = new(1, 1);

    public static void UpdateEntry(Dictionary<string, TrackMetadata> metadata, string path, double rating, List<double> sliders)
    {
        lock (AppState.TrackMetadataLock)
        {
            if (metadata.ContainsKey(path))
            {
                metadata[path].Rating = rating;
                metadata[path].Sliders = sliders;
            }
            else
            {
                metadata.Add(path, new TrackMetadata(rating, sliders));
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
        // Serialize writes so overlapping callers (e.g. rapid volume-slider drags) never collide
        // on the same file, which would otherwise throw a sharing violation and crash the UI.
        await PersistGate.WaitAsync().ConfigureAwait(false);
        try
        {
            string metadataJson;
            lock (AppState.TrackMetadataLock)
            {
                metadataJson = JsonConvert.SerializeObject(metadata);
            }
            await File.WriteAllTextAsync(jsonPath, metadataJson).ConfigureAwait(false);
        }
        finally
        {
            PersistGate.Release();
        }
    }

    public static Dictionary<string, TrackMetadata> Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return new Dictionary<string, TrackMetadata>();

        var loaded = JsonConvert.DeserializeObject<Dictionary<string, TrackMetadata>>(File.ReadAllText(jsonPath));
        return loaded ?? new Dictionary<string, TrackMetadata>();
    }

    public static async Task UpdateAndPersistAsync(
        Dictionary<string, TrackMetadata> metadata,
        string jsonPath,
        string path,
        double rating,
        List<double> sliders)
    {
        UpdateEntry(metadata, path, rating, sliders);
        await PersistAsync(metadata, jsonPath);
    }
}
