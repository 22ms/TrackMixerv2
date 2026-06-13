using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        private Dictionary<MediaPlayer, TypedEventHandler<MediaPlayer, object>> TrackOpenedHandlers = new Dictionary<MediaPlayer, TypedEventHandler<MediaPlayer, object>>();

        private TypedEventHandler<MediaPlayer, object> MediaOpenedHandler;
        private TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> MediaFailedHandler;
        private DispatcherQueue dispatcherQueue;
        public AutoplayMode AutoplayMode = AutoplayMode.Off;
        public event EventHandler<MediaLoadedEventArgs> MediaLoaded;
        public event EventHandler FullScreenToggleRequested;
        public PlaylistConfig PlaylistConfig;

        private string currentVideo;
        private string preChangeVideo;
        private int _openMediaGeneration;
        private DispatcherQueueTimer? _syncTimer;
        private bool _isDisposing;

        public MixedMediaPlayer()
        {
            this.InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            PlaylistConfig = new PlaylistConfig();
            Loaded += MixedMediaPlayer_Loaded;
        }

        private void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            if (_isDisposing)
                return;

            if (sender.CurrentState == MediaPlayerState.Playing)
            {
                SyncTrackPlayersToMain(force: true);
                StartAuxiliaryTracks();
                StartSyncTimer();
            }
            else if (sender.CurrentState == MediaPlayerState.Paused)
            {
                StopSyncTimer();
            }
        }

        private void MediaPlayer_MediaPlayerRateChanged(MediaPlayer sender, MediaPlayerRateChangedEventArgs args)
        {
            double rate = sender.PlaybackRate;
            ForEachAuxiliaryTrack(trackPlayer => trackPlayer.PlaybackRate = rate);

            dispatcherQueue.TryEnqueue(() =>
                customMixedMediaPlayerControl?.SetPlaybackRateSelection(rate));
        }

        private void MixedMediaPlayer_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            RegisterPlayerEvents();
        }

        private void PlaylistConfig_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (currentVideo != null)
                PrewarmPlaylistIndex(PlaylistConfig, currentVideo);

            if (PlaylistConfig.PlaylistMode == PlaylistMode.Chrono)
                return;
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

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                StorageItemThumbnail thumbnail = await currentFile.GetThumbnailAsync(ThumbnailMode.VideosView);
                BitmapImage bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(thumbnail);

                args.DragUI.SetContentFromBitmapImage(bitmapImage);
                args.Data.RequestedOperation = DataPackageOperation.Copy;
                args.AllowedOperations = DataPackageOperation.Copy;

                stopwatch.Stop();
                TimeSpan elapsed = stopwatch.Elapsed;
                Debug.WriteLine($"Thumbnail loading time: {elapsed.TotalMilliseconds} ms");

                args.Data.SetStorageItems(new[] { currentFile }, false);
                PauseAll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving thumbnail: {ex.Message}");
            }
            finally
            {
                deferall.Complete();
            }
        }

        private void CustomMixedMediaPlayerControl_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            RegisterControlEvents();
        }

        private void AutoplayOption_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is not RadioMenuFlyoutItem item || item.Tag is not string option)
                return;

            switch (option)
            {
                case "forward":
                    customMixedMediaPlayerControl.AutoplaySmallIcon.Glyph = "\uEA47";
                    AutoplayMode = AutoplayMode.Forward;
                    break;
                case "backward":
                    customMixedMediaPlayerControl.AutoplaySmallIcon.Glyph = "\uE830";
                    AutoplayMode = AutoplayMode.Backward;
                    break;
                case "off":
                    customMixedMediaPlayerControl.AutoplaySmallIcon.Glyph = "\uE8BB";
                    AutoplayMode = AutoplayMode.Off;
                    break;
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
            ForEachAuxiliaryTrack(trackPlayer => trackPlayer.PlaybackRate = rate);

            customMixedMediaPlayerControl?.SetPlaybackRateSelection(rate);
        }

        private async Task ShowUnsupportedCodecDialogAsync(int mediaGeneration)
        {
            if (UiTestBootstrap.IsEnabled)
                return;

            if (!MediaDialogRules.ShouldShowForOpenGeneration(mediaGeneration, _openMediaGeneration, _isDisposing))
                return;

            if (!MediaDialogRules.ShouldShowCodecFailureDialog(currentVideo, File.Exists(currentVideo)))
                return;

            ContentDialog unsupportedCodecDialog = new ContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Title = "Audio/video codec might be unsupported. Audio/video might not play.",
                Content = "Download the appropiate Audio/video Extension (e.g. AV1, HEVC, VP9, Web Media Extensions) from the Microsoft Store or check the Discord for support.",
                CloseButtonText = "Dismiss"
            };

            await ContentDialogPresenter.TryShowAsync(unsupportedCodecDialog);
        }

        private void RequestUnsupportedCodecDialog(int mediaGeneration)
        {
            if (UiTestBootstrap.IsEnabled)
                return;

            dispatcherQueue.TryEnqueue(() => _ = ShowUnsupportedCodecDialogAsync(mediaGeneration));
        }

        public async void OpenMediaAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                await TryAdvancePastUnavailableMediaAsync(filePath);
                return;
            }

            int generation = ++_openMediaGeneration;
            Dispose();
            _isDisposing = false;
            currentVideo = filePath;
            PlaylistIndexCache.NotifyMediaOpened(filePath, PlaylistConfig.SubfolderOnly);
            PrewarmPlaylistIndex(PlaylistConfig, filePath);
            LocalSettingsStore.SetString(LocalSettingsStore.Keys.RecentVideo, currentVideo);
            StorageFile file;
            try
            {
                file = await StorageFile.GetFileFromPathAsync(filePath);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
            {
                Trace.WriteLine($"OpenMediaAsync failed for '{filePath}': {ex.Message}");
                await TryAdvancePastUnavailableMediaAsync(filePath);
                return;
            }
            MediaSource source = MediaSource.CreateFromStorageFile(file);
            MediaPlaybackItem mainPlaybackItem = new MediaPlaybackItem(source);
            MainMediaPlayer.MediaPlayer.Source = mainPlaybackItem;
            MediaOpenedHandler = new TypedEventHandler<MediaPlayer, object>(async (mainPlayer, e) =>
            {
                if (generation != _openMediaGeneration)
                    return;

                MediaPlaybackItem loadedMedia = mainPlayer.Source as MediaPlaybackItem;
                if (loadedMedia == null) return;

                bool isVideoUnsupported = loadedMedia.VideoTracks?.Count > 0 &&
                    loadedMedia.VideoTracks[0].SupportInfo.DecoderStatus != MediaDecoderStatus.FullySupported;

                bool isAudioUnsupported = loadedMedia.AudioTracks?.Count > 0 &&
                    loadedMedia.AudioTracks[0].SupportInfo.DecoderStatus != MediaDecoderStatus.FullySupported;

                if (isVideoUnsupported || isAudioUnsupported)
                    RequestUnsupportedCodecDialog(generation);

                if (loadedMedia.AudioTracks.Count > 0)
                {
                    loadedMedia.AudioTracks.SelectedIndex = 0;
                }

                if (loadedMedia.AudioTracks.Count > 1)
                {
                    for (int i = 1; i < loadedMedia.AudioTracks.Count; i++)
                    {
                        if (generation != _openMediaGeneration)
                            return;

                        int currentIndex = i;
                        MediaPlayer mediaPlayer = new MediaPlayer();
                        mediaPlayer.AutoPlay = false;
                        MediaSource source = MediaSource.CreateFromStorageFile(file);
                        MediaPlaybackItem mainPlaybackItem = new MediaPlaybackItem(source);
                        mediaPlayer.Source = mainPlaybackItem;
                        MediaOpened.Add(false);
                        var trackOpenedHandler = new TypedEventHandler<MediaPlayer, object>((trackPlayer, o) =>
                        {
                            if (generation != _openMediaGeneration)
                            {
                                CloseTrackPlayer(trackPlayer);
                                return;
                            }

                            (trackPlayer.Source as MediaPlaybackItem).AudioTracks.SelectedIndex = currentIndex;
                            TrackPlayers.Add(trackPlayer);
                            dispatcherQueue.TryEnqueue(() =>
                            {
                                if (generation != _openMediaGeneration)
                                    return;

                                AlignTrackPlayerToMain(trackPlayer);
                            });
                            MediaOpened[currentIndex - 1] = true;
                            if (MediaOpened.All(x => x))
                            {
                                dispatcherQueue.TryEnqueue(() =>
                                {
                                    if (generation != _openMediaGeneration)
                                        return;

                                    SyncTrackPlayersToMain(force: true);
                                    StartAuxiliaryTracks();
                                    List<MediaPlayer> list = new List<MediaPlayer>();
                                    list.Add(MainMediaPlayer.MediaPlayer);
                                    list.AddRange(GetAuxiliaryTrackSnapshot());
                                    MediaLoaded.Invoke(this, new MediaLoadedEventArgs(list, filePath));
                                });
                            }
                        });
                        TrackOpenedHandlers[mediaPlayer] = trackOpenedHandler;
                        mediaPlayer.MediaOpened += trackOpenedHandler;
                    }
                }
                else
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        if (generation != _openMediaGeneration)
                            return;

                        List<MediaPlayer> list = new List<MediaPlayer>();
                        list.Add(MainMediaPlayer.MediaPlayer);
                        MediaLoaded.Invoke(this, new MediaLoadedEventArgs(list, filePath));
                    });
                }
            });

            MediaFailedHandler = (sender, args) =>
            {
                if (!MediaDialogRules.ShouldShowCodecFailureDialog(currentVideo, File.Exists(currentVideo)))
                    return;

                RequestUnsupportedCodecDialog(generation);
            };
            MainMediaPlayer.MediaPlayer.MediaFailed += MediaFailedHandler;

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
            SyncTrackPlayersToMain(force: true);
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

        public void PlayAll()
        {
            SyncTrackPlayersToMain(force: true);
            MainMediaPlayer.MediaPlayer.Play();
            StartAuxiliaryTracks();
            StartSyncTimer();
        }

        public void PauseAll()
        {
            StopSyncTimer();
            MainMediaPlayer.MediaPlayer.Pause();
            ForEachAuxiliaryTrack(trackPlayer => trackPlayer.Pause());
        }

        public void SetFullScreenButtonState(bool isFullScreen)
        {
            if (customMixedMediaPlayerControl?.FullScreenSymbol != null)
                customMixedMediaPlayerControl.FullScreenSymbol.Glyph = isFullScreen
                    ? ((char)Symbol.BackToWindow).ToString()
                    : ((char)Symbol.FullScreen).ToString();
            if (customMixedMediaPlayerControl?.FullScreenButton != null)
                ToolTipService.SetToolTip(customMixedMediaPlayerControl.FullScreenButton, isFullScreen ? "Exit fullscreen" : "Fullscreen");
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            FullScreenToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RegisterControlEvents()
        {
            if (customMixedMediaPlayerControl != null)
            {
                if (customMixedMediaPlayerControl.ProgressSlider != null)
                {
                    customMixedMediaPlayerControl.ProgressSlider.PointerPressed -= ProgressSlider_PointerPressed;
                    customMixedMediaPlayerControl.ProgressSlider.PointerReleased -= ProgressSlider_PointerReleased;
                    customMixedMediaPlayerControl.ProgressSlider.PointerPressed += ProgressSlider_PointerPressed;
                    customMixedMediaPlayerControl.ProgressSlider.PointerReleased += ProgressSlider_PointerReleased;
                }

                if (customMixedMediaPlayerControl.NextTrackButton != null)
                {
                    customMixedMediaPlayerControl.NextTrackButton.Click -= NextTrackButton_Click;
                    customMixedMediaPlayerControl.NextTrackButton.Click += NextTrackButton_Click;
                }
                if (customMixedMediaPlayerControl.PreviousTrackButton != null)
                {
                    customMixedMediaPlayerControl.PreviousTrackButton.Click -= PreviousTrackButton_Click;
                    customMixedMediaPlayerControl.PreviousTrackButton.Click += PreviousTrackButton_Click;
                }
                if (customMixedMediaPlayerControl.AutoplayForwardOption != null)
                {
                    customMixedMediaPlayerControl.AutoplayForwardOption.Click -= AutoplayOption_Click;
                    customMixedMediaPlayerControl.AutoplayForwardOption.Click += AutoplayOption_Click;
                }
                if (customMixedMediaPlayerControl.AutoplayBackwardOption != null)
                {
                    customMixedMediaPlayerControl.AutoplayBackwardOption.Click -= AutoplayOption_Click;
                    customMixedMediaPlayerControl.AutoplayBackwardOption.Click += AutoplayOption_Click;
                }
                if (customMixedMediaPlayerControl.AutoplayOffOption != null)
                {
                    customMixedMediaPlayerControl.AutoplayOffOption.Click -= AutoplayOption_Click;
                    customMixedMediaPlayerControl.AutoplayOffOption.Click += AutoplayOption_Click;
                }
                if (customMixedMediaPlayerControl.FullScreenButton != null)
                {
                    customMixedMediaPlayerControl.FullScreenButton.Click -= FullScreenButton_Click;
                    customMixedMediaPlayerControl.FullScreenButton.Click += FullScreenButton_Click;
                }

                customMixedMediaPlayerControl.EnsureCustomPlaybackRateFlyout();
                if (customMixedMediaPlayerControl.PlaybackRateSlider != null)
                {
                    customMixedMediaPlayerControl.PlaybackRateSlider.ValueChanged -= PlaybackRateSlider_ValueChanged;
                    customMixedMediaPlayerControl.PlaybackRateSlider.ValueChanged += PlaybackRateSlider_ValueChanged;
                }

                UpdateAutoplayFlyoutSelection();
                if (MainMediaPlayer?.MediaPlayer != null)
                    customMixedMediaPlayerControl.SetPlaybackRateSelection(MainMediaPlayer.MediaPlayer.PlaybackRate);

                customMixedMediaPlayerControl.Loaded -= CustomMixedMediaPlayerControl_Loaded;
                customMixedMediaPlayerControl.Loaded += CustomMixedMediaPlayerControl_Loaded;
            }
        }

        private void PlaybackRateSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (customMixedMediaPlayerControl == null || customMixedMediaPlayerControl.IsSyncingPlaybackRateSlider)
                return;

            if (customMixedMediaPlayerControl.TryGetSelectedPlaybackRate(out double rate))
                ChangePlaybackSpeed(rate);
        }

        private void UpdateAutoplayFlyoutSelection()
        {
            if (customMixedMediaPlayerControl == null)
                return;

            switch (AutoplayMode)
            {
                case AutoplayMode.Forward:
                    customMixedMediaPlayerControl.AutoplayForwardOption.IsChecked = true;
                    break;
                case AutoplayMode.Backward:
                    customMixedMediaPlayerControl.AutoplayBackwardOption.IsChecked = true;
                    break;
                default:
                    customMixedMediaPlayerControl.AutoplayOffOption.IsChecked = true;
                    break;
            }
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
            if (customMixedMediaPlayerControl != null)
            {
                if (customMixedMediaPlayerControl.NextTrackButton != null)
                    customMixedMediaPlayerControl.NextTrackButton.Click -= NextTrackButton_Click;
                if (customMixedMediaPlayerControl.PreviousTrackButton != null)
                    customMixedMediaPlayerControl.PreviousTrackButton.Click -= PreviousTrackButton_Click;
                if (customMixedMediaPlayerControl.AutoplayForwardOption != null)
                    customMixedMediaPlayerControl.AutoplayForwardOption.Click -= AutoplayOption_Click;
                if (customMixedMediaPlayerControl.AutoplayBackwardOption != null)
                    customMixedMediaPlayerControl.AutoplayBackwardOption.Click -= AutoplayOption_Click;
                if (customMixedMediaPlayerControl.AutoplayOffOption != null)
                    customMixedMediaPlayerControl.AutoplayOffOption.Click -= AutoplayOption_Click;
                if (customMixedMediaPlayerControl.ProgressSlider != null)
                {
                    customMixedMediaPlayerControl.ProgressSlider.PointerPressed -= ProgressSlider_PointerPressed;
                    customMixedMediaPlayerControl.ProgressSlider.PointerReleased -= ProgressSlider_PointerReleased;
                }
                if (customMixedMediaPlayerControl.FullScreenButton != null)
                    customMixedMediaPlayerControl.FullScreenButton.Click -= FullScreenButton_Click;
                if (customMixedMediaPlayerControl.PlaybackRateSlider != null)
                    customMixedMediaPlayerControl.PlaybackRateSlider.ValueChanged -= PlaybackRateSlider_ValueChanged;
                customMixedMediaPlayerControl.Loaded -= CustomMixedMediaPlayerControl_Loaded;
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
                    MainMediaPlayer.MediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;
                    MainMediaPlayer.MediaPlayer.SystemMediaTransportControls.ButtonPressed += SystemMediaTransportControls_ButtonPressed;
                    MainMediaPlayer.MediaPlayer.SeekCompleted += MediaPlayer_SeekCompleted;
                    MainMediaPlayer.MediaPlayer.MediaPlayerRateChanged += MediaPlayer_MediaPlayerRateChanged;
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
                    MainMediaPlayer.MediaPlayer.CurrentStateChanged -= MediaPlayer_CurrentStateChanged;
                    MainMediaPlayer.MediaPlayer.MediaFailed -= MediaFailedHandler;
                    MainMediaPlayer.MediaPlayer.SystemMediaTransportControls.ButtonPressed -= SystemMediaTransportControls_ButtonPressed;
                    MainMediaPlayer.MediaPlayer.SeekCompleted -= MediaPlayer_SeekCompleted;
                    MainMediaPlayer.MediaPlayer.MediaPlayerRateChanged -= MediaPlayer_MediaPlayerRateChanged;
                    MainMediaPlayer.MediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
                }
                MainMediaPlayer.DragStarting -= MainMediaPlayer_DragStarting;
            }
            foreach (var kvp in TrackOpenedHandlers.ToList())
            {
                kvp.Key.MediaOpened -= kvp.Value;
                CloseTrackPlayer(kvp.Key);
            }
            TrackOpenedHandlers.Clear();
        }

        private static void CloseTrackPlayer(MediaPlayer? trackPlayer)
        {
            if (trackPlayer == null)
                return;

            trackPlayer.Pause();
            trackPlayer.Source = null;
        }

        public static void OffsetMediaPlayerPlaybackPosition(MediaPlayer mediaPlayer, int offsetMillis)
        {
            TimeSpan newPosition = AudioTrackSync.AddOffset(
                mediaPlayer.Position,
                offsetMillis,
                mediaPlayer.NaturalDuration);

            if (newPosition == mediaPlayer.Position)
                return;

            mediaPlayer.Position = newPosition;
        }

        private void AlignTrackPlayerToMain(MediaPlayer trackPlayer)
        {
            if (_isDisposing || !TryGetMainMediaPlayer(out MediaPlayer? mainPlayer))
                return;

            try
            {
                trackPlayer.Position = mainPlayer.Position;
                if (mainPlayer.CurrentState == MediaPlayerState.Playing)
                    trackPlayer.Play();
            }
            catch (COMException)
            {
            }
        }

        private void SyncTrackPlayersToMain(bool force = false)
        {
            if (_isDisposing || TrackPlayers.Count == 0 || !TryGetMainMediaPlayer(out MediaPlayer? mainPlayer))
                return;

            TimeSpan masterPosition;
            try
            {
                masterPosition = mainPlayer.Position;
            }
            catch (COMException)
            {
                return;
            }

            foreach (MediaPlayer trackPlayer in GetAuxiliaryTrackSnapshot())
            {
                try
                {
                    if (force || AudioTrackSync.ShouldResync(
                        masterPosition,
                        trackPlayer.Position,
                        AudioTrackSync.DefaultResyncThreshold))
                    {
                        trackPlayer.Position = masterPosition;
                    }
                }
                catch (COMException)
                {
                }
            }
        }

        private IReadOnlyList<MediaPlayer> GetAuxiliaryTrackSnapshot() => TrackPlayers.ToList();

        private void ForEachAuxiliaryTrack(Action<MediaPlayer> action)
        {
            foreach (MediaPlayer trackPlayer in GetAuxiliaryTrackSnapshot())
                action(trackPlayer);
        }

        private void StartAuxiliaryTracks()
        {
            if (_isDisposing || !TryGetMainMediaPlayer(out MediaPlayer? mainPlayer))
                return;

            MediaPlayerState mainState;
            try
            {
                mainState = mainPlayer.CurrentState;
            }
            catch (COMException)
            {
                return;
            }

            if (mainState != MediaPlayerState.Playing)
                return;

            ForEachAuxiliaryTrack(trackPlayer =>
            {
                try
                {
                    if (trackPlayer.CurrentState != MediaPlayerState.Playing)
                        trackPlayer.Play();
                }
                catch (COMException)
                {
                }
            });
        }

        private async Task TryAdvancePastUnavailableMediaAsync(string? unavailablePath)
        {
            string? anchorPath = string.IsNullOrWhiteSpace(unavailablePath) ? currentVideo : unavailablePath;
            ++_openMediaGeneration;
            Dispose();
            _isDisposing = false;
            currentVideo = null;

            if (string.IsNullOrWhiteSpace(anchorPath))
                return;

            string? nextVideo = await GetTrack(PlaylistConfig, anchorPath, Direction.Next);
            if (nextVideo != null && !string.Equals(nextVideo, anchorPath, StringComparison.OrdinalIgnoreCase))
            {
                dispatcherQueue.TryEnqueue(() => OpenMediaAsync(nextVideo));
                return;
            }

            string? previousVideo = await GetTrack(PlaylistConfig, anchorPath, Direction.Previous);
            if (previousVideo != null && !string.Equals(previousVideo, anchorPath, StringComparison.OrdinalIgnoreCase))
                dispatcherQueue.TryEnqueue(() => OpenMediaAsync(previousVideo));
        }

        private bool TryGetMainMediaPlayer(out MediaPlayer? mediaPlayer)
        {
            mediaPlayer = null;
            if (MainMediaPlayer == null)
                return false;

            try
            {
                mediaPlayer = MainMediaPlayer.MediaPlayer;
                return mediaPlayer != null;
            }
            catch (COMException)
            {
                return false;
            }
        }

        private void StartSyncTimer()
        {
            if (_syncTimer != null)
                return;

            _syncTimer = dispatcherQueue.CreateTimer();
            _syncTimer.Interval = AudioTrackSync.SyncCheckInterval;
            _syncTimer.Tick += SyncTimer_Tick;
            _syncTimer.Start();
        }

        private void StopSyncTimer()
        {
            if (_syncTimer == null)
                return;

            _syncTimer.Stop();
            _syncTimer.Tick -= SyncTimer_Tick;
            _syncTimer = null;
        }

        private void SyncTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            if (_isDisposing || !TryGetMainMediaPlayer(out MediaPlayer? mainPlayer))
                return;

            try
            {
                if (mainPlayer.CurrentState != MediaPlayerState.Playing)
                    return;
            }
            catch (COMException)
            {
                StopSyncTimer();
                return;
            }

            SyncTrackPlayersToMain();
        }

        public void FastForward(int timeMillis)
        {
            OffsetMediaPlayerPlaybackPosition(MainMediaPlayer.MediaPlayer, timeMillis);
            ForEachAuxiliaryTrack(trackPlayer => OffsetMediaPlayerPlaybackPosition(trackPlayer, timeMillis));
        }
        public void Rewind(int timeMillis)
        {
            OffsetMediaPlayerPlaybackPosition(MainMediaPlayer.MediaPlayer, -timeMillis);
            ForEachAuxiliaryTrack(trackPlayer => OffsetMediaPlayerPlaybackPosition(trackPlayer, -timeMillis));
        }

        public void Dispose()
        {
            _isDisposing = true;
            StopSyncTimer();
            DeRegisterPlayerEvents();
            DeRegisterControlEvents();
            MediaOpened = new List<bool>();
            MediaOpenedHandler = null;
            MediaFailedHandler = null;

            if (TryGetMainMediaPlayer(out MediaPlayer? mainPlayer))
            {
                try
                {
                    mainPlayer.Pause();
                    mainPlayer.Source = null;
                }
                catch (COMException)
                {
                }
            }

            if (TrackPlayers != null)
            {
                foreach (MediaPlayer trackPlayer in TrackPlayers)
                    CloseTrackPlayer(trackPlayer);

                TrackPlayers.Clear();
            }
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
            if ((e.OriginalSource as Grid)?.Name != "RootGrid") return;
            ChangePlaybackSpeed(2.0);
        }

        private void MainMediaPlayer_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ChangePlaybackSpeed(1.0);
        }
    }
}
