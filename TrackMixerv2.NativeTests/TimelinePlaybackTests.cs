using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace TrackMixerv2.NativeTests;

/// <summary>
/// Drives real MediaPlayers the way MixedMediaPlayer does: the video player plus one
/// audio-only player per extra track, all on one MediaTimelineController. Players are
/// muted, so this measures the pipeline and never plays sound.
/// </summary>
public sealed class TimelinePlaybackTests
{
    [Theory]
    [InlineData("mkv", 1.0)]
    [InlineData("mkv", 2.0)]
    [InlineData("mp4", 4.0)]
    public async Task All_tracks_stay_on_one_clock_through_play_seek_and_rate(string container, double rate)
    {
        string path = Fixtures.Path(container);
        uint[] streamIds = await Fixtures.AudioStreamIdsAsync(container);
        var file = await StorageFile.GetFileFromPathAsync(path);
        var players = new List<MediaPlayer>();
        var sources = new List<AudioTrackSource>();
        try
        {
            var main = CreateMutedPlayer();
            main.IsVideoFrameServerEnabled = true;
            players.Add(main);
            await OpenAsync(main, new MediaPlaybackItem(MediaSource.CreateFromStorageFile(file)));

            for (int track = 1; track < Fixtures.TrackFrequencies.Length; track++)
            {
                var aux = CreateMutedPlayer();
                var source = new AudioTrackSource(path, streamIds[track]);
                sources.Add(source);
                players.Add(aux);
                await OpenAsync(aux, MediaSource.CreateFromMediaStreamSource(source.MediaStreamSource));
            }

            var timeline = new MediaTimelineController { ClockRate = rate };
            foreach (var player in players)
                player.TimelineController = timeline;

            timeline.Start();
            await Task.Delay(1500);
            AssertAllPlayingTogether(players, timeline);

            timeline.Position = TimeSpan.FromSeconds(5);
            await Task.Delay(1000);
            AssertAllPlayingTogether(players, timeline);
            Assert.InRange(timeline.Position.TotalSeconds, 5, 5 + 1.5 * rate);
        }
        finally
        {
            foreach (var player in players)
                player.Dispose();
            foreach (var source in sources)
                source.Dispose();
        }
    }

    private static void AssertAllPlayingTogether(List<MediaPlayer> players, MediaTimelineController timeline)
    {
        TimeSpan reference = players[0].PlaybackSession.Position;
        foreach (var player in players)
        {
            Assert.Equal(MediaPlaybackState.Playing, player.PlaybackSession.PlaybackState);
            Assert.InRange(Math.Abs((player.PlaybackSession.Position - reference).TotalMilliseconds), 0, 40);
        }

        Assert.Equal(MediaTimelineControllerState.Running, timeline.State);
    }

    private static MediaPlayer CreateMutedPlayer()
    {
        var player = new MediaPlayer { AutoPlay = false, IsMuted = true, Volume = 0 };
        player.CommandManager.IsEnabled = false;
        return player;
    }

    private static async Task OpenAsync(MediaPlayer player, IMediaPlaybackSource source)
    {
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.MediaOpened += (_, _) => opened.TrySetResult();
        player.MediaFailed += (_, args) => opened.TrySetException(new InvalidOperationException(args.ErrorMessage));
        player.Source = source;
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
