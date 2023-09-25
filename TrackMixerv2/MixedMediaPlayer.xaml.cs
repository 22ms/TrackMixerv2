using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using static TrackMixerv2.PlaylistHelper;

namespace TrackMixerv2
{
    public sealed partial class MixedMediaPlayer : Page
    {

        public class MediaLoadedEventArgs : EventArgs
        {
            public List<MediaPlayer> TrackPlayers { get; set; }
            public string path;
            public MediaLoadedEventArgs(List<MediaPlayer> trackPlayers, string path)
            {
                TrackPlayers = trackPlayers;
                this.path = path;
            }
        }

        private List<MediaPlayer> TrackPlayers = new List<MediaPlayer>();
        private TypedEventHandler<MediaPlayer, object> TrackOpenedHandler;
        private TypedEventHandler<MediaPlayer, object> MediaOpenedHandler;
        private DispatcherQueue dispatcherQueue;
        private AutoplayMode autoplayMode = AutoplayMode.Off;
        public event EventHandler<MediaLoadedEventArgs> MediaLoaded;
        public PlaylistConfig PlaylistConfig;

        private string currentVideo; // subject to change
        private string preChangeVideo; // subject to change

        public MixedMediaPlayer() 
        {
            this.InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            MainMediaPlayer.DragStarting += MainMediaPlayer_DragStarting;
            myMixedMediaPlayerControl.Loaded += MyMixedMediaPlayerControl_Loaded;
            PlaylistConfig = new PlaylistConfig();
            PlaylistConfig.PropertyChanged += PlaylistConfig_PropertyChanged;
        }

        private void PlaylistConfig_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Debug.WriteLine(PlaylistConfig.PlaylistMode);
            Debug.WriteLine(PlaylistConfig.TimeSpan);
            if (PlaylistConfig.PlaylistMode == PlaylistMode.Chrono) 
            {
                return;
                //if (preChangeVideo == null) return;
                //dispatcherQueue.TryEnqueue(() =>
                //{
                //    Dispose();
                //    OpenMediaAsync(preChangeVideo);
                //    preChangeVideo = null;
                //});
            }
            string newVideo = IsInRatings(PlaylistConfig, currentVideo);
            if (newVideo == null) return;
            preChangeVideo = currentVideo;
            dispatcherQueue.TryEnqueue(() =>
            {
                Dispose();
                OpenMediaAsync(newVideo);
            });
        }

        private async void MainMediaPlayer_DragStarting(Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.DragStartingEventArgs args)
        {
            var deferall = args.GetDeferral();
            StorageFile currentFile = await StorageFile.GetFileFromPathAsync(currentVideo);

            // Use a Stopwatch to measure the time
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                // Get the thumbnail for the video file
                StorageItemThumbnail thumbnail = await currentFile.GetThumbnailAsync(ThumbnailMode.VideosView);
                BitmapImage bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(thumbnail);

                args.DragUI.SetContentFromBitmapImage(bitmapImage);
                args.Data.RequestedOperation = DataPackageOperation.Copy;
                args.AllowedOperations = DataPackageOperation.Copy;
                
                // Calculate and print the time elapsed
                stopwatch.Stop();
                TimeSpan elapsed = stopwatch.Elapsed;
                Debug.WriteLine($"Thumbnail loading time: {elapsed.TotalMilliseconds} ms");

                args.Data.SetStorageItems(new[] { currentFile }, false);
                PauseAll();
            }
            catch (Exception ex)
            {
                // Handle the exception or log it for debugging
                Debug.WriteLine($"Error retrieving thumbnail: {ex.Message}");
            }
            finally
            {
                deferall.Complete();
            }
        } // DONT TOUCH?




        private void MyMixedMediaPlayerControl_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            myMixedMediaPlayerControl.NextTrackButton.Click += NextTrackButton_Click;
            myMixedMediaPlayerControl.PreviousTrackButton.Click += PreviousTrackButton_Click;
            myMixedMediaPlayerControl.AutoplayForwardOption.Click += AutoplayOption_Click;
            myMixedMediaPlayerControl.AutoplayBackwardOption.Click += AutoplayOption_Click;
            myMixedMediaPlayerControl.AutoplayOffOption.Click += AutoplayOption_Click;
        }

        private void AutoplayOption_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            if (item != null)
            {
                string option = item.Tag.ToString();
                switch (option)
                {
                    case "forward":
                        myMixedMediaPlayerControl.AutoplaySmallIcon.Glyph = (myMixedMediaPlayerControl.AutoplayForwardOption.Icon as FontIcon).Glyph;
                        autoplayMode = AutoplayMode.Forward;
                        break;
                    case "backward":
                        myMixedMediaPlayerControl.AutoplaySmallIcon.Glyph = (myMixedMediaPlayerControl.AutoplayBackwardOption.Icon as FontIcon).Glyph;
                        autoplayMode = AutoplayMode.Backward;
                        break;
                    case "off":
                        myMixedMediaPlayerControl.AutoplaySmallIcon.Glyph = (myMixedMediaPlayerControl.AutoplayOffOption.Icon as FontIcon).Glyph;
                        autoplayMode = AutoplayMode.Off;
                        break;
                }
            }
        }

        private void PreviousTrackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (currentVideo == null) return;
            PlayPreviousTrack();
        }

        private void NextTrackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (currentVideo == null) return;
            PlayNextTrack();
        }

        public async void OpenMediaAsync(string filePath)
        {
            currentVideo = filePath;
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            MediaSource source = MediaSource.CreateFromStorageFile(file);
            MediaPlaybackItem mainPlaybackItem = new MediaPlaybackItem(source);
            MainMediaPlayer.MediaPlayer.Source = mainPlaybackItem;
            MediaOpenedHandler = new TypedEventHandler<MediaPlayer, object>((mainPlayer, e) =>
            {
                MediaPlaybackItem loadedMedia = mainPlayer.Source as MediaPlaybackItem;
                if (loadedMedia == null) return;
                if (loadedMedia.AudioTracks.Count > 0) loadedMedia.AudioTracks.SelectedIndex = 0;
                for (int i = 1; i < loadedMedia.AudioTracks.Count; i++)
                {
                    int currentIndex = i;
                    MediaPlayer mediaPlayer = new MediaPlayer();
                    mediaPlayer.AutoPlay = true;
                    MediaSource source = MediaSource.CreateFromStorageFile(file);
                    MediaPlaybackItem mainPlaybackItem = new MediaPlaybackItem(source);
                    mediaPlayer.Source = mainPlaybackItem;
                    TrackOpenedHandler = new TypedEventHandler<MediaPlayer, object>((trackPlayer, o) =>
                    {
                        (trackPlayer.Source as MediaPlaybackItem).AudioTracks.SelectedIndex = currentIndex;
                        TrackPlayers.Add(trackPlayer);
                        if (currentIndex >= loadedMedia.AudioTracks.Count - 1)
                        {
                            dispatcherQueue.TryEnqueue(() => {
                                List<MediaPlayer> list = new List<MediaPlayer>();
                                list.Add(MainMediaPlayer.MediaPlayer);
                                list.AddRange(TrackPlayers);
                                MediaLoaded.Invoke(this, new MediaLoadedEventArgs(list, filePath));
                            });
                        }
                    });
                    mediaPlayer.MediaOpened += TrackOpenedHandler;
                }
            });
            MainMediaPlayer.MediaPlayer.MediaOpened += MediaOpenedHandler;
            MainMediaPlayer.MediaPlayer.SystemMediaTransportControls.ButtonPressed += SystemMediaTransportControls_ButtonPressed;
            MainMediaPlayer.MediaPlayer.SeekCompleted += MediaPlayer_SeekCompleted;
            MainMediaPlayer.MediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            switch(autoplayMode)
            {
                case AutoplayMode.Off:
                    break;
                case AutoplayMode.Forward:
                    PlayNextTrack();
                    break;
                case AutoplayMode.Backward:
                    PlayPreviousTrack();
                    break;
            }
        }

        private void MediaPlayer_SeekCompleted(MediaPlayer sender, object args)
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
            volume = volume / 100;
            if(trackIndex == 0)
            {
                MainMediaPlayer.MediaPlayer.Volume = volume;
                return;
            }
            TrackPlayers[trackIndex-1].Volume = volume;
        }

        public void PlayNextTrack()
        {
            string nextVideo = GetTrack(PlaylistConfig, currentVideo, Direction.Next);
            if (nextVideo == null) return;

            dispatcherQueue.TryEnqueue(() =>
            {
                Dispose();
                OpenMediaAsync(nextVideo);
            });
        }

        public void PlayPreviousTrack()
        {
            string nextVideo = GetTrack(PlaylistConfig, currentVideo, Direction.Previous);
            if(nextVideo == null) return;

            dispatcherQueue.TryEnqueue(() =>
            {
                Dispose();
                OpenMediaAsync(nextVideo);
            });
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

        public void Dispose()
        {
            MainMediaPlayer.MediaPlayer.MediaOpened -= MediaOpenedHandler;
            MainMediaPlayer.MediaPlayer.SystemMediaTransportControls.ButtonPressed -= SystemMediaTransportControls_ButtonPressed;
            MainMediaPlayer.MediaPlayer.SeekCompleted -= MediaPlayer_SeekCompleted;
            MainMediaPlayer.MediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
            MainMediaPlayer.MediaPlayer.Pause();
            //MainMediaPlayer.MediaPlayer.Dispose();
            foreach (MediaPlayer trackPlayer in TrackPlayers)
            {
                trackPlayer.MediaOpened -= TrackOpenedHandler;
                trackPlayer.Pause();
                trackPlayer.Source = null;
                //trackPlayer.Dispose();
            }
            TrackPlayers.Clear();
            GC.Collect();
        }
    }
}
