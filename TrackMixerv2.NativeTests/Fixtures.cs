using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace TrackMixerv2.NativeTests;

/// <summary>Matches scripts/generate-uitest-fixture.ps1.</summary>
internal static class Fixtures
{
    public static readonly double[] TrackFrequencies = [220, 440, 660, 880];
    public static readonly string[] TrackTitles = ["Mic", "Discord", "Game", "System"];
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(20);

    public static string Path(string container) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", $"uitest-4track.{container}");

    /// <summary>AudioTrack.Id per track, in container order (what the app shows as sliders).</summary>
    public static async Task<uint[]> AudioStreamIdsAsync(string container)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path(container));
        using var player = new MediaPlayer { AutoPlay = false, IsMuted = true, Volume = 0 };
        var item = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(file));
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.MediaOpened += (_, _) => opened.TrySetResult();
        player.MediaFailed += (_, args) => opened.TrySetException(new InvalidOperationException(args.ErrorMessage));
        player.Source = item;
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(TrackTitles, item.AudioTracks.Select(track => track.Name).ToArray());
        return item.AudioTracks.Select(track => uint.Parse(track.Id)).ToArray();
    }

    /// <summary>Frequency of channel 0 estimated from upward zero crossings.</summary>
    public static double EstimateFrequency(IReadOnlyList<float> interleaved, int channels, uint sampleRate)
    {
        int crossings = 0;
        int frames = interleaved.Count / channels;
        for (int frame = 1; frame < frames; frame++)
        {
            if (interleaved[(frame - 1) * channels] < 0 && interleaved[frame * channels] >= 0)
                crossings++;
        }

        return crossings * (double)sampleRate / frames;
    }
}
