using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace TrackMixerv2
{
    public sealed partial class MixedMediaPlayer : Page
    {
        public class MediaLoadedEventArgs : EventArgs
        {
            public int AudioTrackCount { get; set; }
            public MediaLoadedEventArgs(int audioTrackCount)
            {
                AudioTrackCount = audioTrackCount;
            }
        }

        private List<MediaPlayer> TrackPlayers = new List<MediaPlayer>();
        private DispatcherQueue dispatcherQueue;
        public event EventHandler<MediaLoadedEventArgs> MediaLoaded;
        public MixedMediaPlayer() 
        {
            this.InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        public async void OpenMediaAsync(string filePath)
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            MediaSource source = MediaSource.CreateFromStorageFile(file);
            MediaPlaybackItem mainPlaybackItem = new MediaPlaybackItem(source);
            MainMediaPlayer.Source = mainPlaybackItem;
            MainMediaPlayer.MediaPlayer.MediaOpened += (mainPlayer, e) =>
            {
                MediaPlaybackItem loadedMedia = mainPlayer.Source as MediaPlaybackItem;
                if (loadedMedia == null) return;
                if (loadedMedia.AudioTracks.Count > 0) loadedMedia.AudioTracks.SelectedIndex = 0;
                for (int i = 1; i < loadedMedia.AudioTracks.Count; i++)
                {
                    int currentIndex = i;
                    MediaPlayer mediaPlayer = new MediaPlayer();
                    MediaPlaybackItem trackPlaybackItem = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(file));
                    mediaPlayer.Source = trackPlaybackItem;
                    mediaPlayer.MediaOpened += (trackPlayer, o) =>
                    {
                        (mediaPlayer.Source as MediaPlaybackItem).AudioTracks.SelectedIndex = currentIndex;
                        TrackPlayers.Add(mediaPlayer);
                        if(currentIndex >= loadedMedia.AudioTracks.Count)
                        {
                            MediaLoaded.Invoke(this, new MediaLoadedEventArgs(loadedMedia.AudioTracks.Count));
                        }
                    };
                }
            };
            MainMediaPlayer.MediaPlayer.SystemMediaTransportControls.ButtonPressed += SystemMediaTransportControls_ButtonPressed;
            MainMediaPlayer.MediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
        }

        private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
        {
            foreach (MediaPlayer trackPlayer in TrackPlayers)
            {
                trackPlayer.Position = sender.Position;
            }
        }

        private void SystemMediaTransportControls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    dispatcherQueue.TryEnqueue(() => PlayAll());
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    dispatcherQueue.TryEnqueue(() => PauseAll());
                    break;
            }
        }

        public void SetVolume(int trackIndex, double volume)
        {
            Debug.WriteLine(trackIndex);
            Debug.WriteLine(volume);
            return;
            if(trackIndex == 0)
            {
                MainMediaPlayer.MediaPlayer.Volume = volume;
                return;
            }
            TrackPlayers[trackIndex].Volume = volume;
        }

        public void PlayAll()
        {
            MainMediaPlayer.MediaPlayer.Play(); 
            foreach (MediaPlayer trackPlayer in TrackPlayers)
            {
                trackPlayer.Play();
            }
        }

        public void PauseAll()
        {
            MainMediaPlayer.MediaPlayer.Pause();
            foreach (MediaPlayer trackPlayer in TrackPlayers)
            {
                trackPlayer.Pause();
            }
        }
    }
}
