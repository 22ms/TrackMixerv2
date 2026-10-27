#nullable enable
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    /// <summary>
    /// Plays a video with all of its audio tracks at once. Track 0 plays in the video's own
    /// player; each further track gets an audio-only player (AudioTrackSource never decodes
    /// video). All players run on one MediaTimelineController, so they share a single clock
    /// and never need re-seeking to stay in sync. Under a timeline controller
    /// MediaPlayer.Play/Pause/Position throw, so this class drives the transport itself.
    /// </summary>
    public sealed partial class MixedMediaPlayer : Page
    {
        public sealed class MediaLoadedEventArgs(string path, IReadOnlyList<string> trackNames) : EventArgs
        {
            public string Path { get; } = path;
            public IReadOnlyList<string> TrackNames { get; } = trackNames;
        }

        private sealed record TrackPlayer(MediaPlayer Player, AudioTrackSource? AudioOnlySource);

        private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(15);
        private const long ScrubSeekIntervalMs = 150;
        private const double RightClickHoldRate = 2.0;

        private readonly DispatcherQueue dispatcherQueue;
        private readonly DispatcherQueueTimer _progressTimer;
        public AutoplayMode AutoplayMode = AutoplayMode.Off;
        public PlaylistConfig PlaylistConfig { get; } = new();

        /// <summary>Raised when a file starts opening, before anything is loaded.</summary>
        public event EventHandler<string>? MediaOpening;

        /// <summary>
        /// Raised once every track is open and before playback starts, so handlers can apply
        /// saved volumes before anything is audible.
        /// </summary>
        public event EventHandler<MediaLoadedEventArgs>? MediaLoaded;

        public event EventHandler? PlaybackStateChanged;
        public event EventHandler? FullScreenToggleRequested;

        private string? currentVideo;
        private int _openGeneration;
        private MediaTimelineController? _timeline;
        private MediaPlayer? _mainPlayer;
        private TrackPlayer?[] _trackPlayers = [];
        private TimeSpan _duration;
        private TimeSpan? _seekTarget;
        private long _seekTick;
        private double _baseRate = 1.0;
        private double? _temporaryRate;
        private bool _rightHoldActive;
        private bool _scrubbing;
        private bool _resumeAfterScrub;
        private bool _updatingProgress;
        private long _lastScrubSeekTick;
        private bool _controlEventsHooked;

        public MixedMediaPlayer()
        {
            InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _progressTimer = dispatcherQueue.CreateTimer();
            _progressTimer.Interval = TimeSpan.FromMilliseconds(200);
            _progressTimer.Tick += (_, _) => UpdateTransport();
            PlaylistConfig.PropertyChanged += PlaylistConfig_PropertyChanged;
            MainMediaPlayer.DragStarting += MainMediaPlayer_DragStarting;
            customMixedMediaPlayerControl.Loaded += (_, _) => HookControlEvents();
            if (UiTestBootstrap.IsEnabled)
                PlaybackDiagnostics.Visibility = Visibility.Visible;
        }

        public string? CurrentVideo => currentVideo;

        public bool IsPlaying => _timeline?.State == MediaTimelineControllerState.Running;

        public double PlaybackRate => _temporaryRate ?? _baseRate;

        public TimeSpan Position
        {
            get
            {
                if (_timeline == null)
                    return TimeSpan.Zero;

                TimeSpan position = _timeline.Position;
                // The controller reports a new Position only about a millisecond after it is
                // set, so two quick skips would both start from the old position.
                if (_seekTarget is TimeSpan target)
                {
                    if (Math.Abs((position - target).TotalMilliseconds) < 250 || Environment.TickCount64 - _seekTick > 1000)
                        _seekTarget = null;
                    else
                        position = target;
                }

                if (position < TimeSpan.Zero)
                    return TimeSpan.Zero;
                return _duration > TimeSpan.Zero && position > _duration ? _duration : position;
            }
        }

        public async void OpenMedia(string filePath)
        {
            int generation = ++_openGeneration;
            try
            {
                await OpenMediaCoreAsync(filePath, generation);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Opening '{filePath}' failed: {ex}");
            }
        }

        private async Task OpenMediaCoreAsync(string filePath, int generation)
        {
            CloseMedia();
            currentVideo = filePath;
            MediaOpening?.Invoke(this, filePath);

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                await AdvancePastUnavailableAsync(filePath, generation);
                return;
            }

            PlaylistIndexCache.NotifyMediaOpened(filePath, PlaylistConfig.SubfolderOnly);
            PrewarmPlaylistIndex(PlaylistConfig, filePath);

            StorageFile file;
            try
            {
                file = await StorageFile.GetFileFromPathAsync(filePath);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
            {
                Trace.WriteLine($"Opening '{filePath}' failed: {ex.Message}");
                if (generation == _openGeneration)
                    await AdvancePastUnavailableAsync(filePath, generation);
                return;
            }

            if (generation != _openGeneration)
                return;

            var mainPlayer = CreatePlayer();
            var trackPlayers = new List<TrackPlayer?>();
            bool owned = false;
            try
            {
                var mainItem = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(file));
                bool opened = await OpenAsync(mainPlayer, mainItem);
                if (generation != _openGeneration)
                    return;

                if (!opened || IsUnsupported(mainItem))
                    RequestUnsupportedCodecDialog(generation);
                if (!opened)
                    return;

                List<AudioTrack> audioTracks = mainItem.AudioTracks.ToList();
                if (audioTracks.Count > 0)
                    mainItem.AudioTracks.SelectedIndex = 0;
                List<string> trackNames = audioTracks
                    .Select((track, index) => string.IsNullOrWhiteSpace(track.Name) ? $"Track {index + 1}" : track.Name)
                    .ToList();

                trackPlayers.AddRange(await Task.WhenAll(
                    audioTracks.Skip(1).Select((track, index) => OpenTrackPlayerAsync(filePath, file, track, index + 1))));
                if (generation != _openGeneration)
                    return;

                var timeline = new MediaTimelineController { ClockRate = PlaybackRate };
                // Start/Pause/Resume change State asynchronously, so the transport follows the event.
                timeline.StateChanged += (sender, _) => dispatcherQueue.TryEnqueue(() =>
                {
                    if (ReferenceEquals(_timeline, sender))
                        NotifyPlaybackState();
                });
                mainPlayer.TimelineController = timeline;
                foreach (TrackPlayer? track in trackPlayers)
                {
                    if (track != null)
                        track.Player.TimelineController = timeline;
                }

                _mainPlayer = mainPlayer;
                _trackPlayers = trackPlayers.ToArray();
                _timeline = timeline;
                _duration = mainPlayer.PlaybackSession.NaturalDuration;
                owned = true;

                MainMediaPlayer.SetMediaPlayer(mainPlayer);
                mainPlayer.MediaEnded += (_, _) => dispatcherQueue.TryEnqueue(() =>
                {
                    if (generation == _openGeneration)
                        OnMediaEnded();
                });
                mainPlayer.MediaFailed += (_, _) => RequestUnsupportedCodecDialog(generation);
                ConfigureSystemMediaControls(mainPlayer, filePath, generation);

                MediaLoaded?.Invoke(this, new MediaLoadedEventArgs(filePath, trackNames));
                if (generation != _openGeneration)
                    return;

                timeline.Start();
                NotifyPlaybackState();
                UpdateTransport();
                _progressTimer.Start();
            }
            finally
            {
                if (!owned)
                {
                    DisposePlayer(mainPlayer);
                    foreach (TrackPlayer? track in trackPlayers)
                        DisposeTrack(track);
                }
            }
        }

        private static async Task<TrackPlayer?> OpenTrackPlayerAsync(string path, StorageFile file, AudioTrack track, int trackIndex)
        {
            var player = CreatePlayer();
            if (uint.TryParse(track.Id, out uint streamIndex))
            {
                AudioTrackSource? source = null;
                try
                {
                    source = await Task.Run(() => new AudioTrackSource(path, streamIndex));
                    if (await OpenAsync(player, MediaSource.CreateFromMediaStreamSource(source.MediaStreamSource)))
                        return new TrackPlayer(player, source);
                }
                catch (Exception ex) when (ex is COMException or ArgumentOutOfRangeException)
                {
                    Trace.WriteLine($"Audio-only open of track {trackIndex} failed, falling back: {ex.Message}");
                }

                source?.Dispose();
            }

            // Fallback: the whole container with this track selected. This decodes the video a
            // second time, so it is only used when Media Foundation can't read the track alone.
            player.IsVideoFrameServerEnabled = true;
            var item = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(file));
            if (await OpenAsync(player, item))
            {
                item.AudioTracks.SelectedIndex = trackIndex;
                return new TrackPlayer(player, null);
            }

            DisposePlayer(player);
            return null;
        }

        private static MediaPlayer CreatePlayer()
        {
            var player = new MediaPlayer { AutoPlay = false };
            player.CommandManager.IsEnabled = false;
            return player;
        }

        private static async Task<bool> OpenAsync(MediaPlayer player, IMediaPlaybackSource source)
        {
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TypedEventHandler<MediaPlayer, object> opened = (_, _) => result.TrySetResult(true);
            TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> failed = (_, args) =>
            {
                Trace.WriteLine($"Media failed: {args.Error} {args.ErrorMessage}");
                result.TrySetResult(false);
            };

            player.MediaOpened += opened;
            player.MediaFailed += failed;
            try
            {
                player.Source = source;
                return await result.Task.WaitAsync(OpenTimeout);
            }
            catch (TimeoutException)
            {
                return false;
            }
            finally
            {
                player.MediaOpened -= opened;
                player.MediaFailed -= failed;
            }
        }

        private static bool IsUnsupported(MediaPlaybackItem item) =>
            (item.VideoTracks.Count > 0 && item.VideoTracks[0].SupportInfo.DecoderStatus != MediaDecoderStatus.FullySupported)
            || (item.AudioTracks.Count > 0 && item.AudioTracks[0].SupportInfo.DecoderStatus != MediaDecoderStatus.FullySupported);

        /// <summary>Stops playback and releases every player and file handle.</summary>
        public void CloseMedia()
        {
            _progressTimer.Stop();
            _scrubbing = false;

            _timeline?.Pause();
            _timeline = null;

            MediaPlayer? mainPlayer = _mainPlayer;
            _mainPlayer = null;
            if (mainPlayer != null)
            {
                try
                {
                    MainMediaPlayer.SetMediaPlayer(null);
                }
                catch (Exception ex) when (ex is COMException or ArgumentException)
                {
                }

                DisposePlayer(mainPlayer);
            }

            foreach (TrackPlayer? track in _trackPlayers)
                DisposeTrack(track);
            _trackPlayers = [];
            _duration = TimeSpan.Zero;
            _seekTarget = null;
            NotifyPlaybackState();
            UpdateTransport();
        }

        public void Dispose()
        {
            ++_openGeneration;
            CloseMedia();
            PlaylistConfig.PropertyChanged -= PlaylistConfig_PropertyChanged;
        }

        private static void DisposeTrack(TrackPlayer? track)
        {
            if (track == null)
                return;

            DisposePlayer(track.Player);
            track.AudioOnlySource?.Dispose();
        }

        private static void DisposePlayer(MediaPlayer player)
        {
            try
            {
                player.Source = null;
                player.Dispose();
            }
            catch (COMException)
            {
            }
        }

        public void Play()
        {
            if (_timeline == null)
                return;

            if (_duration > TimeSpan.Zero && Position >= _duration - TimeSpan.FromMilliseconds(50))
                SetTimelinePosition(TimeSpan.Zero);

            _timeline.Resume();
            NotifyPlaybackState();
        }

        public void Pause()
        {
            if (_timeline == null)
                return;

            _timeline.Pause();
            NotifyPlaybackState();
        }

        public void TogglePlayPause()
        {
            if (IsPlaying)
                Pause();
            else
                Play();
        }

        public void Seek(TimeSpan position)
        {
            if (_timeline == null)
                return;

            if (position < TimeSpan.Zero)
                position = TimeSpan.Zero;
            if (_duration > TimeSpan.Zero && position > _duration)
                position = _duration;

            SetTimelinePosition(position);
            UpdateTransport();
        }

        private void SetTimelinePosition(TimeSpan position)
        {
            _timeline!.Position = position;
            _seekTarget = position;
            _seekTick = Environment.TickCount64;
        }

        public void FastForward(int timeMillis) =>
            Seek(SeekMath.AddOffset(Position, timeMillis, _duration));

        public void Rewind(int timeMillis) =>
            Seek(SeekMath.AddOffset(Position, -timeMillis, _duration));

        /// <summary>The speed picked in the speed flyout; it survives video changes.</summary>
        public void SetPlaybackRate(double rate)
        {
            _baseRate = rate;
            ApplyPlaybackRate();
        }

        /// <summary>A hold speed (keyboard or right mouse button); ending it restores the picked speed.</summary>
        public void BeginTemporaryRate(double rate)
        {
            _temporaryRate = rate;
            ApplyPlaybackRate();
        }

        public void EndTemporaryRate()
        {
            if (_temporaryRate == null)
                return;

            _temporaryRate = null;
            ApplyPlaybackRate();
        }

        private void ApplyPlaybackRate()
        {
            if (_timeline != null)
                _timeline.ClockRate = PlaybackRate;
            customMixedMediaPlayerControl?.SetPlaybackRateSelection(PlaybackRate);
        }

        public void SetVolume(int trackIndex, double volume)
        {
            volume /= 100;
            if (trackIndex == 0)
            {
                if (_mainPlayer != null)
                    _mainPlayer.Volume = volume;
                return;
            }

            if (trackIndex > 0 && trackIndex <= _trackPlayers.Length && _trackPlayers[trackIndex - 1] is TrackPlayer track)
                track.Player.Volume = volume;
        }

        private void OnMediaEnded()
        {
            if (!IsPlaying)
                return;

            _timeline!.Pause();
            SetTimelinePosition(_duration);
            NotifyPlaybackState();
            UpdateTransport();

            switch (AutoplayMode)
            {
                case AutoplayMode.Forward:
                    PlayNextTrack();
                    break;
                case AutoplayMode.Backward:
                    PlayPreviousTrack();
                    break;
            }
        }

        public void PlayNextTrack() => _ = NavigateAsync(Direction.Next);

        public void PlayPreviousTrack() => _ = NavigateAsync(Direction.Previous);

        private async Task NavigateAsync(Direction direction)
        {
            try
            {
                string? target = await GetTrack(PlaylistConfig, currentVideo, direction);
                if (target != null)
                    OpenMedia(target);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Playlist navigation failed: {ex}");
            }
        }

        private async Task AdvancePastUnavailableAsync(string? unavailablePath, int generation)
        {
            currentVideo = null;
            if (string.IsNullOrWhiteSpace(unavailablePath))
                return;

            foreach (Direction direction in new[] { Direction.Next, Direction.Previous })
            {
                string? target = await GetTrack(PlaylistConfig, unavailablePath, direction);
                if (generation != _openGeneration)
                    return;

                if (target != null && !string.Equals(target, unavailablePath, StringComparison.OrdinalIgnoreCase))
                {
                    OpenMedia(target);
                    return;
                }
            }
        }

        private void PlaylistConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (currentVideo != null)
                PrewarmPlaylistIndex(PlaylistConfig, currentVideo);

            if (PlaylistConfig.PlaylistMode == PlaylistMode.Chrono)
                return;

            string? ratedVideo = IsInRatings(PlaylistConfig, currentVideo);
            if (ratedVideo != null)
                dispatcherQueue.TryEnqueue(() => OpenMedia(ratedVideo));
        }

        private void NotifyPlaybackState()
        {
            customMixedMediaPlayerControl?.SetPlaying(IsPlaying);
            if (_mainPlayer != null)
            {
                _mainPlayer.SystemMediaTransportControls.PlaybackStatus = IsPlaying
                    ? MediaPlaybackStatus.Playing
                    : MediaPlaybackStatus.Paused;
            }

            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ConfigureSystemMediaControls(MediaPlayer player, string path, int generation)
        {
            // With the CommandManager off (required by the timeline controller) the media keys
            // and the Windows media flyout are this class's job.
            SystemMediaTransportControls controls = player.SystemMediaTransportControls;
            controls.IsEnabled = true;
            controls.IsPlayEnabled = true;
            controls.IsPauseEnabled = true;
            controls.DisplayUpdater.Type = MediaPlaybackType.Video;
            controls.DisplayUpdater.VideoProperties.Title = Path.GetFileName(path);
            controls.DisplayUpdater.Update();
            controls.ButtonPressed += (_, args) => dispatcherQueue.TryEnqueue(() =>
            {
                if (generation != _openGeneration)
                    return;

                if (args.Button == SystemMediaTransportControlsButton.Play)
                    Play();
                else if (args.Button == SystemMediaTransportControlsButton.Pause)
                    Pause();
            });
        }

        private void UpdateTransport()
        {
            MixedMediaPlayerControl control = customMixedMediaPlayerControl;
            if (control == null)
                return;

            // The template can be applied after Loaded; hooking is idempotent.
            HookControlEvents();

            TimeSpan position = Position;
            if (_timeline != null && IsPlaying && _duration > TimeSpan.Zero && _timeline.Position >= _duration)
            {
                // Safety net in case MediaEnded never arrives: the controller keeps running past the end.
                OnMediaEnded();
                return;
            }

            Slider? slider = control.ProgressSlider;
            if (slider != null && !_scrubbing)
            {
                _updatingProgress = true;
                slider.Maximum = Math.Max(_duration.TotalSeconds, 0);
                slider.Value = position.TotalSeconds;
                _updatingProgress = false;
            }

            control.SetTimes(_scrubbing && slider != null ? TimeSpan.FromSeconds(slider.Value) : position, _duration);

            if (UiTestBootstrap.IsEnabled)
                UpdateDiagnostics(position);
        }

        private void UpdateDiagnostics(TimeSpan position)
        {
            var tracks = new List<object>();
            if (_mainPlayer != null)
                tracks.Add(DescribePlayer(0, _mainPlayer, "video"));
            for (int i = 0; i < _trackPlayers.Length; i++)
            {
                if (_trackPlayers[i] is TrackPlayer track)
                    tracks.Add(DescribePlayer(i + 1, track.Player, track.AudioOnlySource != null ? "audio-only" : "fallback"));
                else
                    tracks.Add(new { index = i + 1, kind = "missing" });
            }

            PlaybackDiagnostics.Text = JsonSerializer.Serialize(new
            {
                path = currentVideo,
                state = _timeline?.State.ToString() ?? "None",
                position = position.TotalSeconds,
                duration = _duration.TotalSeconds,
                rate = PlaybackRate,
                tracks,
            });
        }

        private static object DescribePlayer(int index, MediaPlayer player, string kind)
        {
            try
            {
                return new
                {
                    index,
                    kind,
                    position = player.PlaybackSession.Position.TotalSeconds,
                    state = player.PlaybackSession.PlaybackState.ToString(),
                    volume = player.Volume,
                };
            }
            catch (COMException)
            {
                return new { index, kind, state = "Error" };
            }
        }

        private void HookControlEvents()
        {
            MixedMediaPlayerControl control = customMixedMediaPlayerControl;
            if (_controlEventsHooked || control?.ProgressSlider == null)
                return;

            _controlEventsHooked = true;
            if (control.PlayPauseButton != null)
                control.PlayPauseButton.Click += (_, _) => TogglePlayPause();
            if (control.PlayPauseButtonOnLeft != null)
                control.PlayPauseButtonOnLeft.Click += (_, _) => TogglePlayPause();
            if (control.NextTrackButton != null)
                control.NextTrackButton.Click += (_, _) => PlayNextTrack();
            if (control.PreviousTrackButton != null)
                control.PreviousTrackButton.Click += (_, _) => PlayPreviousTrack();
            if (control.FullScreenButton != null)
                control.FullScreenButton.Click += (_, _) => FullScreenToggleRequested?.Invoke(this, EventArgs.Empty);
            foreach (RadioMenuFlyoutItem? option in new[] { control.AutoplayForwardOption, control.AutoplayBackwardOption, control.AutoplayOffOption })
            {
                if (option != null)
                    option.Click += AutoplayOption_Click;
            }

            control.EnsureCustomPlaybackRateFlyout();
            if (control.PlaybackRateSlider != null)
                control.PlaybackRateSlider.ValueChanged += PlaybackRateSlider_ValueChanged;

            Slider slider = control.ProgressSlider;
            slider.Minimum = 0;
            slider.SmallChange = 1;
            slider.LargeChange = 10;
            slider.StepFrequency = 0.1;
            slider.ValueChanged += ProgressSlider_ValueChanged;
            // The slider marks pointer events handled while it drags, so listen to handled ones too.
            slider.AddHandler(PointerPressedEvent, new PointerEventHandler(ProgressSlider_PointerPressed), true);
            slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(ProgressSlider_PointerReleased), true);
            slider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(ProgressSlider_PointerReleased), true);

            UpdateAutoplayFlyoutSelection();
            control.SetPlaybackRateSelection(PlaybackRate);
            control.SetPlaying(IsPlaying);
            UpdateTransport();
        }

        private void ProgressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_timeline == null || _scrubbing)
                return;

            _scrubbing = true;
            _resumeAfterScrub = IsPlaying;
            _lastScrubSeekTick = 0;
            Pause();
        }

        private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_scrubbing)
                return;

            _scrubbing = false;
            Seek(TimeSpan.FromSeconds(((Slider)sender).Value));
            if (_resumeAfterScrub)
                Play();
        }

        // Clicks, drags, the mouse wheel (SliderWheelBehavior: SmallChange is one second) and
        // the keyboard all arrive here; programmatic progress updates are filtered out.
        private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_updatingProgress || _timeline == null)
                return;

            if (_scrubbing)
            {
                long now = Environment.TickCount64;
                if (now - _lastScrubSeekTick < ScrubSeekIntervalMs)
                    return;
                _lastScrubSeekTick = now;
            }

            Seek(TimeSpan.FromSeconds(e.NewValue));
        }

        private void PlaybackRateSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            MixedMediaPlayerControl control = customMixedMediaPlayerControl;
            if (control == null || control.IsSyncingPlaybackRateSlider)
                return;

            if (control.TryGetSelectedPlaybackRate(out double rate))
                SetPlaybackRate(rate);
        }

        private void AutoplayOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioMenuFlyoutItem item || item.Tag is not string option)
                return;

            AutoplayMode = option switch
            {
                "forward" => AutoplayMode.Forward,
                "backward" => AutoplayMode.Backward,
                _ => AutoplayMode.Off,
            };
            UpdateAutoplayFlyoutSelection();
        }

        private void UpdateAutoplayFlyoutSelection()
        {
            MixedMediaPlayerControl control = customMixedMediaPlayerControl;
            if (control?.AutoplaySmallIcon == null)
                return;

            switch (AutoplayMode)
            {
                case AutoplayMode.Forward:
                    control.AutoplaySmallIcon.Glyph = "";
                    control.AutoplayForwardOption.IsChecked = true;
                    break;
                case AutoplayMode.Backward:
                    control.AutoplaySmallIcon.Glyph = "";
                    control.AutoplayBackwardOption.IsChecked = true;
                    break;
                default:
                    control.AutoplaySmallIcon.Glyph = "";
                    control.AutoplayOffOption.IsChecked = true;
                    break;
            }
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

        private async void MainMediaPlayer_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            string? video = currentVideo;
            if (video == null || !File.Exists(video))
            {
                args.Cancel = true;
                return;
            }

            var deferral = args.GetDeferral();
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(video);
                StorageItemThumbnail thumbnail = await file.GetThumbnailAsync(ThumbnailMode.VideosView);
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);

                args.DragUI.SetContentFromBitmapImage(bitmap);
                args.Data.RequestedOperation = DataPackageOperation.Copy;
                args.AllowedOperations = DataPackageOperation.Copy;
                args.Data.SetStorageItems(new[] { file }, false);
                Pause();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Drag of '{video}' failed: {ex.Message}");
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void MainMediaPlayer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
                return;
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsRightButtonPressed)
                return;
            // Only on the video itself, not on the transport bar.
            if ((e.OriginalSource as Grid)?.Name != "RootGrid")
                return;

            _rightHoldActive = true;
            BeginTemporaryRate(RightClickHoldRate);
        }

        private void MainMediaPlayer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_rightHoldActive)
                return;

            _rightHoldActive = false;
            EndTemporaryRate();
        }

        private void RequestUnsupportedCodecDialog(int mediaGeneration)
        {
            if (UiTestBootstrap.IsEnabled)
                return;

            dispatcherQueue.TryEnqueue(() => _ = ShowUnsupportedCodecDialogAsync(mediaGeneration));
        }

        private async Task ShowUnsupportedCodecDialogAsync(int mediaGeneration)
        {
            if (!MediaDialogRules.ShouldShowForOpenGeneration(mediaGeneration, _openGeneration))
                return;

            if (!MediaDialogRules.ShouldShowCodecFailureDialog(currentVideo, File.Exists(currentVideo)))
                return;

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Audio/video codec might be unsupported. Audio/video might not play.",
                Content = "Download the appropiate Audio/video Extension (e.g. AV1, HEVC, VP9, Web Media Extensions) from the Microsoft Store or check the Discord for support.",
                CloseButtonText = "Dismiss"
            };

            await ContentDialogPresenter.ShowAsync(dialog);
        }
    }
}
