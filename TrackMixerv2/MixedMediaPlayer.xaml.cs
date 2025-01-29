using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
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
        private List<bool> MediaOpened = new List<bool>();

        private TypedEventHandler<MediaPlayer, object> TrackOpenedHandler;
        private TypedEventHandler<MediaPlayer, object> MediaOpenedHandler;
        private DispatcherQueue dispatcherQueue;
        public AutoplayMode AutoplayMode = AutoplayMode.Off;
        public event EventHandler<MediaLoadedEventArgs> MediaLoaded;
        public PlaylistConfig PlaylistConfig;

        private string currentVideo; // subject to change
        private string preChangeVideo; // subject to change

        public MixedMediaPlayer()
        {
            this.InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            PlaylistConfig = new PlaylistConfig();
            Loaded += MixedMediaPlayer_Loaded;
        }

        private void MediaPlayer_MediaPlayerRateChanged(MediaPlayer sender, MediaPlayerRateChangedEventArgs args)
        {
            foreach (MediaPlayer trackPlayer in TrackPlayers)
            {
                trackPlayer.PlaybackRate = sender.PlaybackRate;
            }
        }

        private void MixedMediaPlayer_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            RegisterPlayerEvents();
        }

        private void PlaylistConfig_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
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
            RegisterControlEvents();
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
                        AutoplayMode = AutoplayMode.Forward;
                        break;
                    case "backward":
                        myMixedMediaPlayerControl.AutoplaySmallIcon.Glyph = (myMixedMediaPlayerControl.AutoplayBackwardOption.Icon as FontIcon).Glyph;
                        AutoplayMode = AutoplayMode.Backward;
                        break;
                    case "off":
                        myMixedMediaPlayerControl.AutoplaySmallIcon.Glyph = (myMixedMediaPlayerControl.AutoplayOffOption.Icon as FontIcon).Glyph;
                        AutoplayMode = AutoplayMode.Off;
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

        public void ChangePlaybackSpeed(double rate)
        {
            if (currentVideo == null) return;
            MainMediaPlayer.MediaPlayer.PlaybackRate = rate;
            foreach (MediaPlayer trackPlayer in TrackPlayers)
            {
                trackPlayer.PlaybackRate = rate;
            }
        }

        public async void OpenMediaAsync(string filePath)
        {
            // cleanup previous video
            Dispose();
            //
            currentVideo = filePath;
            ApplicationData.Current.LocalSettings.Values["RecentVideo"] = currentVideo;
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            MediaSource source = MediaSource.CreateFromStorageFile(file);
            MediaPlaybackItem mainPlaybackItem = new MediaPlaybackItem(source);
            MainMediaPlayer.MediaPlayer.Source = mainPlaybackItem;
            MediaOpenedHandler = new TypedEventHandler<MediaPlayer, object>((mainPlayer, e) =>
            {
                MediaPlaybackItem loadedMedia = mainPlayer.Source as MediaPlaybackItem;
                if (loadedMedia == null) return;
                if (loadedMedia.AudioTracks.Count > 0) loadedMedia.AudioTracks.SelectedIndex = 0;

                if (loadedMedia.AudioTracks.Count > 1)
                {
                    for (int i = 1; i < loadedMedia.AudioTracks.Count; i++)
                    {
                        int currentIndex = i;
                        MediaPlayer mediaPlayer = new MediaPlayer();
                        mediaPlayer.AutoPlay = true;
                        MediaSource source = MediaSource.CreateFromStorageFile(file);
                        MediaPlaybackItem mainPlaybackItem = new MediaPlaybackItem(source);
                        mediaPlayer.Source = mainPlaybackItem;
                        MediaOpened.Add(false);
                        TrackOpenedHandler = new TypedEventHandler<MediaPlayer, object>((trackPlayer, o) =>
                        {
                            (trackPlayer.Source as MediaPlaybackItem).AudioTracks.SelectedIndex = currentIndex;
                            TrackPlayers.Add(trackPlayer);
                            MediaOpened[currentIndex - 1] = true;
                            if (MediaOpened.All(x => x))
                            {
                                dispatcherQueue.TryEnqueue(() =>
                                {
                                    List<MediaPlayer> list = new List<MediaPlayer>();
                                    list.Add(MainMediaPlayer.MediaPlayer);
                                    list.AddRange(TrackPlayers);
                                    MediaLoaded.Invoke(this, new MediaLoadedEventArgs(list, filePath));
                                });
                            }
                        });
                        mediaPlayer.MediaOpened += TrackOpenedHandler;
                    }
                }
                else
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        List<MediaPlayer> list = new List<MediaPlayer>();
                        list.Add(MainMediaPlayer.MediaPlayer);
                        MediaLoaded.Invoke(this, new MediaLoadedEventArgs(list, filePath));
                    });
                }
            });
            RegisterPlayerEvents();
            RegisterControlEvents();
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            switch (AutoplayMode)
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
            if (trackIndex == 0)
            {
                MainMediaPlayer.MediaPlayer.Volume = volume;
                return;
            }

            if (TrackPlayers != null && trackIndex > 0 && trackIndex <= TrackPlayers.Count)
            {
                TrackPlayers[trackIndex - 1].Volume = volume;
            }
        }

        public async void PlayNextTrack()
        {
            string nextVideo = await GetTrack(PlaylistConfig, currentVideo, Direction.Next);
            if (nextVideo == null) return;

            dispatcherQueue.TryEnqueue(() =>
            {
                OpenMediaAsync(nextVideo);
            });
        }

        public async void PlayPreviousTrack()
        {
            string previousVideo = await GetTrack(PlaylistConfig, currentVideo, Direction.Previous);
            if (previousVideo == null) return;

            dispatcherQueue.TryEnqueue(() =>
            {
                OpenMediaAsync(previousVideo);
            });
        }

        private async Task<bool> VerifyRootFolders()
        {
            if (MainWindow.ROOT_FOLDERS == null || MainWindow.ROOT_FOLDERS.Count == 0)
            {
                return await MainWindow.Instance.AddNewRootFolder();
            }
            return true;
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

        public void RegisterControlEvents()
        {
            if (myMixedMediaPlayerControl != null)
            {
                // Right click pause fix
                if (myMixedMediaPlayerControl.ProgressSlider != null)
                {
                    myMixedMediaPlayerControl.ProgressSlider.PointerPressed += ProgressSlider_PointerPressed;
                    myMixedMediaPlayerControl.ProgressSlider.PointerReleased += ProgressSlider_PointerReleased;
                }

                if (myMixedMediaPlayerControl.NextTrackButton != null)
                    myMixedMediaPlayerControl.NextTrackButton.Click += NextTrackButton_Click;
                if (myMixedMediaPlayerControl.PreviousTrackButton != null)
                    myMixedMediaPlayerControl.PreviousTrackButton.Click += PreviousTrackButton_Click;
                if (myMixedMediaPlayerControl.AutoplayForwardOption != null)
                    myMixedMediaPlayerControl.AutoplayForwardOption.Click += AutoplayOption_Click;
                if (myMixedMediaPlayerControl.AutoplayBackwardOption != null)
                    myMixedMediaPlayerControl.AutoplayBackwardOption.Click += AutoplayOption_Click;
                if (myMixedMediaPlayerControl.AutoplayOffOption != null)
                    myMixedMediaPlayerControl.AutoplayOffOption.Click += AutoplayOption_Click;
                myMixedMediaPlayerControl.Loaded += MyMixedMediaPlayerControl_Loaded;
            }
        }

        private void PlaybackRateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Trace.WriteLine(MainMediaPlayer.MediaPlayer.PlaybackRate);
        }

        private void ProgressSlider_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            PauseAll();
        }

        private void ProgressSlider_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            PlayAll();
        }

        public void DeRegisterControlEvents()
        {
            if (myMixedMediaPlayerControl != null)
            {
                if (myMixedMediaPlayerControl.NextTrackButton != null)
                    myMixedMediaPlayerControl.NextTrackButton.Click -= NextTrackButton_Click;
                if (myMixedMediaPlayerControl.PreviousTrackButton != null)
                    myMixedMediaPlayerControl.PreviousTrackButton.Click -= PreviousTrackButton_Click;
                if (myMixedMediaPlayerControl.AutoplayForwardOption != null)
                    myMixedMediaPlayerControl.AutoplayForwardOption.Click -= AutoplayOption_Click;
                if (myMixedMediaPlayerControl.AutoplayBackwardOption != null)
                    myMixedMediaPlayerControl.AutoplayBackwardOption.Click -= AutoplayOption_Click;
                if (myMixedMediaPlayerControl.AutoplayOffOption != null)
                    myMixedMediaPlayerControl.AutoplayOffOption.Click -= AutoplayOption_Click;
                myMixedMediaPlayerControl.Loaded -= MyMixedMediaPlayerControl_Loaded;
            }
        }

        public void RegisterPlayerEvents()
        {
            if (PlaylistConfig != null)
                PlaylistConfig.PropertyChanged += PlaylistConfig_PropertyChanged;
            if (MainMediaPlayer != null)
            {
                if (MainMediaPlayer.MediaPlayer != null)
                {
                    MainMediaPlayer.MediaPlayer.MediaOpened += MediaOpenedHandler;
                    MainMediaPlayer.MediaPlayer.SystemMediaTransportControls.ButtonPressed += SystemMediaTransportControls_ButtonPressed;
                    MainMediaPlayer.MediaPlayer.SeekCompleted += MediaPlayer_SeekCompleted;
                    MainMediaPlayer.MediaPlayer.MediaPlayerRateChanged += MediaPlayer_MediaPlayerRateChanged;
                    //MainMediaPlayer.MediaPlayer.SystemMediaTransportControls.
                    MainMediaPlayer.MediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                }
                MainMediaPlayer.DragStarting += MainMediaPlayer_DragStarting;
            }
        }

        public void DeRegisterPlayerEvents()
        {
            if (PlaylistConfig != null)
                PlaylistConfig.PropertyChanged -= PlaylistConfig_PropertyChanged;
            if (MainMediaPlayer != null)
            {
                if (MainMediaPlayer.MediaPlayer != null)
                {
                    MainMediaPlayer.MediaPlayer.MediaOpened -= MediaOpenedHandler;
                    MainMediaPlayer.MediaPlayer.SystemMediaTransportControls.ButtonPressed -= SystemMediaTransportControls_ButtonPressed;
                    MainMediaPlayer.MediaPlayer.SeekCompleted -= MediaPlayer_SeekCompleted;
                    MainMediaPlayer.MediaPlayer.MediaPlayerRateChanged -= MediaPlayer_MediaPlayerRateChanged;
                    MainMediaPlayer.MediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
                }
                MainMediaPlayer.DragStarting -= MainMediaPlayer_DragStarting;
            }
            if (TrackPlayers != null)
            {
                foreach (var trackPlayer in TrackPlayers)
                {
                    trackPlayer.MediaOpened -= TrackOpenedHandler;
                }
            }
        }

        public static void OffsetMediaPlayerPlaybackPosition(MediaPlayer mediaPlayer, int offsetMillis)
        {
            TimeSpan currentPosition = mediaPlayer.Position;
            TimeSpan fastForwardTime = TimeSpan.FromMilliseconds(offsetMillis);
            TimeSpan newPosition = currentPosition + fastForwardTime;

            if (newPosition >= mediaPlayer.NaturalDuration)
            {
                return;
            }

            mediaPlayer.Position = newPosition;
        }

        public void FastForward(int timeMillis)
        {
            OffsetMediaPlayerPlaybackPosition(MainMediaPlayer.MediaPlayer, timeMillis);
            foreach (MediaPlayer trackPlayer in TrackPlayers)
            {
                OffsetMediaPlayerPlaybackPosition(trackPlayer, timeMillis);
            }
        }
        public void Rewind(int timeMillis)
        {
            OffsetMediaPlayerPlaybackPosition(MainMediaPlayer.MediaPlayer, -timeMillis);
            foreach (MediaPlayer trackPlayer in TrackPlayers)
            {
                OffsetMediaPlayerPlaybackPosition(trackPlayer, -timeMillis);
            }
        }

        public void Dispose()
        {
            DeRegisterPlayerEvents();
            DeRegisterControlEvents();
            MediaOpened = new List<bool>();
            if (MainMediaPlayer?.MediaPlayer != null)
            {
                MainMediaPlayer.MediaPlayer.Pause();
                //MainMediaPlayer.MediaPlayer.Dispose();
            }
            if (TrackPlayers != null)
            {
                foreach (MediaPlayer trackPlayer in TrackPlayers)
                {
                    if (trackPlayer != null)
                    {
                        trackPlayer.Pause();
                        trackPlayer.Source = null;
                        //trackPlayer.Dispose();
                    }
                }
                TrackPlayers.Clear();
            }
            GC.Collect();
        }

        private void MainMediaPlayer_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var pointerPoint = e.GetCurrentPoint((UIElement)sender);
                if (!pointerPoint.Properties.IsRightButtonPressed) return;
            }
            else
            {
                return;
            }
            if ((e.OriginalSource as Grid).Name != "RootGrid") return;
            ChangePlaybackSpeed(2.0);
        }

        private void MainMediaPlayer_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ChangePlaybackSpeed(1.0);
        }
    }
}
