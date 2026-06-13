using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using static TrackMixerv2.MixedMediaPlayer;
using static TrackMixerv2.PlaylistHelper;

namespace TrackMixerv2
{
    public sealed partial class MixerPage : Page
    {
        Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;
        TabViewItem tabViewItem;
        bool initialLoaded = false;
        CancellationTokenSource keyboardPollCts;
        List<RangeBaseValueChangedEventHandler> VolumeSliderChangedHandlers = new List<RangeBaseValueChangedEventHandler>();
        List<RangeBaseValueChangedEventHandler> FlyoutVolumeSliderChangedHandlers = new List<RangeBaseValueChangedEventHandler>();
        List<Slider> VolumeSliders = new List<Slider>();
        List<Slider> FlyoutVolumeSliders = new List<Slider>();
        bool syncingVolumeSliders;
        private const double FlyoutVolumeTrackLabelWidth = 96;
        List<double> CachedSliderValues = new List<double>();
        private bool suppressRatingSave;
        private long ratingValueChangedToken;
        private KeybindAction? _recordingKeybindAction;
        private TextBlock? _recordingKeybindShortcutBlock;
        private Border? _recordingKeybindCell;
        private bool _mixerToolsInTransport;
        private TextBlock? _exportButtonLabel;
        private TextBlock? _deleteButtonLabel;
        private Thickness _exportButtonPadding;
        private Thickness _deleteButtonPadding;
        private HorizontalAlignment _exportHorizontalAlignment;
        private HorizontalAlignment _deleteHorizontalAlignment;
        private double _exportMinWidth;
        private double _deleteMinWidth;
        private Thickness _playlistControlsMargin;
        private double _ratingSliderWidth;
        private double _ratingSliderMinWidth;
        private double _ratingSliderMaxWidth;
        private double _ratingControlsMinWidth;
        bool syncingSuppressRootFolderPromptCheckBox;
        public string path;
        public MixerPage(string path)
        {
            this.InitializeComponent();
            dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            this.path = path;
            VideoTitle.Text = Helper.GetTitleFromPath(path);
            MixedMediaPlayer.Loaded += MixedMediaPlayer_Loaded;
            MixedMediaPlayer.FullScreenToggleRequested += MixedMediaPlayer_FullScreenToggleRequested;
            MixedMediaPlayer.MainMediaPlayer.MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            ratingValueChangedToken = RatingSlider.RegisterPropertyChangedCallback(RangeBase.ValueProperty, OnRatingValueChanged);
            PlaylistFilterTimeValue.ValueChanged += PlaylistFilterTimeValue_ValueChanged;
            PlaylistFilterTimeUnit.SelectionChanged += PlaylistFilterTimeUnit_SelectionChanged;
            PlaylistFilterChrono.Click += PlaylistFilterMode_Click;
            PlaylistFilterRating.Click += PlaylistFilterMode_Click;
            PlaylistSubfolderToggle.Click += PlaylistSubfolderToggle_Click;
            LoadMetadata(path);
            Loaded += MixerPage_Loaded;
        }

        private void MixerPage_Loaded(object sender, RoutedEventArgs e)
        {
            KeybindApplicator.ApplyToMixerPage(this);
            KeybindRecordingCapture.PreviewKeyDown += KeybindRecordingCapture_PreviewKeyDown;
            RefreshKeybindList();
        }

        private void KeybindRecordingCapture_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (TryHandleKeybindRecording(e))
                e.Handled = true;
        }
        private async void Preferences_Click(object sender, RoutedEventArgs e)
        {
            SkipSecondsNumberBox.Value = LocalSettingsStore.GetSkipSeconds();
            SliderWheelSpeedNumberBox.Value = LocalSettingsStore.GetSliderWheelSpeed();

            var result = await PreferencesDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                LocalSettingsStore.SetBool(LocalSettingsStore.Keys.DragAndDropOnNewTab, DragAndDropCheckBox.IsChecked ?? false);
                LocalSettingsStore.SetBool(LocalSettingsStore.Keys.DoubleClickOnNewTab, DoubleClickCheckBox.IsChecked ?? false);
                LocalSettingsStore.SetSkipSeconds((int)Math.Round(SkipSecondsNumberBox.Value));
                LocalSettingsStore.SetSliderWheelSpeed((int)Math.Round(SliderWheelSpeedNumberBox.Value));
                RefreshKeybindList();
            }
        }

        private void MixedMediaPlayer_FullScreenToggleRequested(object sender, EventArgs e)
        {
            MainWindow.Instance?.TogglePlayerFullScreen(MixedMediaPlayer);
        }

        public void SetFullscreenTransportToolsVisible(bool visible)
        {
            var control = MixedMediaPlayer?.customMixedMediaPlayerControl;
            if (control?.FullscreenMixerToolsHost == null)
                return;

            if (visible && !_mixerToolsInTransport)
                MoveMixerToolsToTransport(control);
            else if (!visible && _mixerToolsInTransport)
                RestoreMixerToolsToBottomPanel();
        }

        private void MoveMixerToolsToTransport(MixedMediaPlayerControl control)
        {
            _exportButtonLabel ??= GetButtonLabel(ExportButton);
            _deleteButtonLabel ??= GetButtonLabel(DeleteVideoButton);

            ExportDeleteRow.Children.Remove(ExportButton);
            ExportDeleteRow.Children.Remove(DeleteVideoButton);
            RatingPlaylistRow.Children.Remove(RatingControlsGrid);
            RatingPlaylistRow.Children.Remove(PlaylistControlsStack);

            control.FullscreenMixerToolsLeadingHost.Children.Add(ExportButton);
            control.FullscreenMixerToolsLeadingHost.Children.Add(DeleteVideoButton);
            control.FullscreenMixerToolsTrailingHost.Children.Add(RatingControlsGrid);
            control.FullscreenMixerToolsTrailingHost.Children.Add(PlaylistControlsStack);

            control.FullscreenMixerToolsHost.Visibility = Visibility.Visible;
            ApplyFullscreenCompactStyles();
            _mixerToolsInTransport = true;
        }

        private void RestoreMixerToolsToBottomPanel()
        {
            var control = MixedMediaPlayer?.customMixedMediaPlayerControl;
            if (control == null)
                return;

            control.FullscreenMixerToolsLeadingHost.Children.Remove(ExportButton);
            control.FullscreenMixerToolsLeadingHost.Children.Remove(DeleteVideoButton);
            control.FullscreenMixerToolsTrailingHost.Children.Remove(RatingControlsGrid);
            control.FullscreenMixerToolsTrailingHost.Children.Remove(PlaylistControlsStack);

            ExportDeleteRow.Children.Add(ExportButton);
            Grid.SetColumn(ExportButton, 0);
            ExportDeleteRow.Children.Add(DeleteVideoButton);
            Grid.SetColumn(DeleteVideoButton, 1);

            RatingPlaylistRow.Children.Add(RatingControlsGrid);
            Grid.SetColumn(RatingControlsGrid, 0);
            RatingPlaylistRow.Children.Add(PlaylistControlsStack);
            Grid.SetColumn(PlaylistControlsStack, 1);

            control.FullscreenMixerToolsHost.Visibility = Visibility.Collapsed;
            RestoreFullscreenCompactStyles();
            _mixerToolsInTransport = false;
        }

        private static TextBlock? GetButtonLabel(Button button)
        {
            if (button.Content is StackPanel stackPanel
                && stackPanel.Children.Count > 1
                && stackPanel.Children[1] is TextBlock label)
            {
                return label;
            }

            return null;
        }

        private void ApplyFullscreenCompactStyles()
        {
            _exportButtonPadding = ExportButton.Padding;
            _exportHorizontalAlignment = ExportButton.HorizontalAlignment;
            _exportMinWidth = ExportButton.MinWidth;
            ExportButton.Padding = new Thickness(8);
            ExportButton.HorizontalAlignment = HorizontalAlignment.Center;
            ExportButton.VerticalAlignment = VerticalAlignment.Center;
            ExportButton.MinWidth = 36;
            ExportButton.MinHeight = 36;
            if (_exportButtonLabel != null)
                _exportButtonLabel.Visibility = Visibility.Collapsed;

            _deleteButtonPadding = DeleteVideoButton.Padding;
            _deleteHorizontalAlignment = DeleteVideoButton.HorizontalAlignment;
            _deleteMinWidth = DeleteVideoButton.MinWidth;
            DeleteVideoButton.Padding = new Thickness(8);
            DeleteVideoButton.HorizontalAlignment = HorizontalAlignment.Center;
            DeleteVideoButton.VerticalAlignment = VerticalAlignment.Center;
            DeleteVideoButton.MinWidth = 36;
            DeleteVideoButton.MinHeight = 36;
            if (_deleteButtonLabel != null)
                _deleteButtonLabel.Visibility = Visibility.Collapsed;

            _ratingSliderWidth = RatingSlider.Width;
            _ratingSliderMinWidth = RatingSlider.MinWidth;
            _ratingSliderMaxWidth = RatingSlider.MaxWidth;
            _ratingControlsMinWidth = RatingControlsGrid.MinWidth;
            RatingSlider.Width = 112;
            RatingSlider.MinWidth = 96;
            RatingSlider.MaxWidth = 128;
            RatingControlsGrid.MinWidth = 140;

            _playlistControlsMargin = PlaylistControlsStack.Margin;
            PlaylistControlsStack.Margin = new Thickness(0);
        }

        private void RestoreFullscreenCompactStyles()
        {
            ExportButton.Padding = _exportButtonPadding;
            ExportButton.HorizontalAlignment = _exportHorizontalAlignment;
            ExportButton.MinWidth = _exportMinWidth;
            ExportButton.MinHeight = 0;
            ExportButton.VerticalAlignment = VerticalAlignment.Stretch;
            if (_exportButtonLabel != null)
                _exportButtonLabel.Visibility = Visibility.Visible;

            DeleteVideoButton.Padding = _deleteButtonPadding;
            DeleteVideoButton.HorizontalAlignment = _deleteHorizontalAlignment;
            DeleteVideoButton.MinWidth = _deleteMinWidth;
            DeleteVideoButton.MinHeight = 0;
            DeleteVideoButton.VerticalAlignment = VerticalAlignment.Stretch;
            if (_deleteButtonLabel != null)
                _deleteButtonLabel.Visibility = Visibility.Visible;

            RatingSlider.Width = _ratingSliderWidth;
            RatingSlider.MinWidth = _ratingSliderMinWidth;
            RatingSlider.MaxWidth = _ratingSliderMaxWidth;
            RatingControlsGrid.MinWidth = _ratingControlsMinWidth;

            PlaylistControlsStack.Margin = _playlistControlsMargin;
        }

        private async void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var options = new LauncherOptions {};
                await Launcher.LaunchUriAsync(uri, options);
            }
        }
        private void DeleteConfirmationFlyout_Opened(object sender, object e)
        {
            MixedMediaPlayer.PauseAll();
        }

        private static async Task PlaybackKeyboardCheck(MixedMediaPlayer mixedMediaPlayer, Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue, CancellationToken cancellationToken)
        {
            bool previousBoostKeyDown = false;
            bool previousSlowKeyDown = false;

            try
            {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!MainWindow.MainWindowActivated)
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                VirtualKey boostKey = (VirtualKey)KeybindStore.Get(KeybindAction.SpeedBoost).Key;
                VirtualKey slowKey = (VirtualKey)KeybindStore.Get(KeybindAction.SpeedSlow).Key;
                bool boostKeyDown = IsKeyDown(boostKey);
                bool slowKeyDown = IsKeyDown(slowKey);

                if (boostKeyDown != previousBoostKeyDown || slowKeyDown != previousSlowKeyDown)
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        if (IsKeyDown(boostKey))
                            mixedMediaPlayer.ChangePlaybackSpeed(2.0);
                        else if (IsKeyDown(slowKey))
                            mixedMediaPlayer.ChangePlaybackSpeed(0.25);
                        else
                            mixedMediaPlayer.ChangePlaybackSpeed(1.0);
                    });
                    previousBoostKeyDown = boostKeyDown;
                    previousSlowKeyDown = slowKeyDown;
                }

                await Task.Delay(10, cancellationToken);
            }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
        private void DeleteVideoConfirmation_Click(object sender, RoutedEventArgs e)
        {
            switch (MixedMediaPlayer.AutoplayMode)
            {
                case AutoplayMode.Off:
                    MixedMediaPlayer.PlayNextTrack();
                    break;
                case AutoplayMode.Forward:
                    MixedMediaPlayer.PlayNextTrack();
                    break;
                case AutoplayMode.Backward:
                    MixedMediaPlayer.PlayPreviousTrack();
                    break;
            }
            try
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            MixedMediaPlayer.PlayAll();
            DeleteConfirmationFlyout.Hide();
        }
        private void DeleteConfirmationFlyout_Closed(object sender, object e)
        {
            MixedMediaPlayer.PlayAll();
            DeleteConfirmationFlyout.Hide();
        }

        private void CancelDelete_Click(object sender, RoutedEventArgs e)
        {
            MixedMediaPlayer.PlayAll();
            DeleteConfirmationFlyout.Hide();
        }
        private void PlaylistSubfolderToggle_Click(object sender, RoutedEventArgs e)
        {
            MixedMediaPlayer.PlaylistConfig.SubfolderOnly = PlaylistSubfolderToggle.IsChecked.GetValueOrDefault();
            PrewarmPlaylistIndex(MixedMediaPlayer.PlaylistConfig, path);
        }

        private void PlaylistFilterTimeUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MixedMediaPlayer.PlaylistConfig.TimeSpan = TimeSpanFromUnitValue((TimeUnit)PlaylistFilterTimeUnit.SelectedIndex, PlaylistFilterTimeValue.Value);
        }

        private void PlaylistFilterTimeValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            MixedMediaPlayer.PlaylistConfig.TimeSpan = TimeSpanFromUnitValue((TimeUnit)PlaylistFilterTimeUnit.SelectedIndex, PlaylistFilterTimeValue.Value);
        }

        private void PlaylistFilterMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioMenuFlyoutItem item || item.Tag is not string tag)
                return;

            switch (tag)
            {
                case "chrono":
                    PlaylistFilterTimeValue.Visibility = Visibility.Collapsed;
                    PlaylistFilterTimeUnit.Visibility = Visibility.Collapsed;
                    PlaylistFilterSelectionIcon.Glyph = "\uE81C";
                    MixedMediaPlayer.PlaylistConfig.PlaylistMode = PlaylistMode.Chrono;
                    break;
                case "rating":
                    PlaylistFilterTimeValue.Visibility = Visibility.Visible;
                    PlaylistFilterTimeUnit.Visibility = Visibility.Visible;
                    PlaylistFilterSelectionIcon.Glyph = "\uE728";

                    MixedMediaPlayer.PlaylistConfig.PlaylistMode = PlaylistMode.Rating;
                    MixedMediaPlayer.PlaylistConfig.TimeSpan = TimeSpanFromUnitValue((TimeUnit)PlaylistFilterTimeUnit.SelectedIndex, PlaylistFilterTimeValue.Value);
                    break;
            }
        }

        public void OpenNewMedia(string path)
        {
            this.path = path;
            MixedMediaPlayer.Dispose();
            MixedMediaPlayer.OpenMediaAsync(path);
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Debug.WriteLine(args.Error);
        }

        private void MixedMediaPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            keyboardPollCts = new CancellationTokenSource();
            var pollToken = keyboardPollCts.Token;
            Task.Run(() => PlaybackKeyboardCheck(MixedMediaPlayer, dispatcherQueue, pollToken), pollToken);
            dispatcherQueue.TryEnqueue(() =>
            {
                if (initialLoaded) return;
                if (tabViewItem == null)
                    tabViewItem = Parent as TabViewItem;
                MixedMediaPlayer.OpenMediaAsync(path);
                MixedMediaPlayer.MediaLoaded += MixedMediaPlayer_MediaLoaded;
                MixedMediaPlayer.MainMediaPlayer.MediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;

                if (LocalSettingsStore.ContainsKey(LocalSettingsStore.Keys.DragAndDropOnNewTab))
                    DragAndDropCheckBox.IsChecked = LocalSettingsStore.GetBool(LocalSettingsStore.Keys.DragAndDropOnNewTab);
                else
                    DragAndDropCheckBox.IsChecked = true;

                if (LocalSettingsStore.ContainsKey(LocalSettingsStore.Keys.DoubleClickOnNewTab))
                    DoubleClickCheckBox.IsChecked = LocalSettingsStore.GetBool(LocalSettingsStore.Keys.DoubleClickOnNewTab);
                else
                    DoubleClickCheckBox.IsChecked = true;

                SkipSecondsNumberBox.Value = LocalSettingsStore.GetSkipSeconds();
                SliderWheelSpeedNumberBox.Value = LocalSettingsStore.GetSliderWheelSpeed();

                initialLoaded = true;
            });
        }

        private void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                if (sender.CurrentState == MediaPlayerState.Playing)
                    tabViewItem.IconSource = new SymbolIconSource() { Symbol = Symbol.Volume };
                else
                {
                    tabViewItem.IconSource = new SymbolIconSource() { Symbol = Symbol.SlideShow };
                }

            });
        }

        private void MixedMediaPlayer_MediaLoaded(object sender, MediaLoadedEventArgs args)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                this.path = args.path;
                // HACK
                tabViewItem.IsEnabled = false;
                tabViewItem.IsEnabled = true;
                // HACK END xd
                VideoTitle.Text = Helper.GetTitleFromPath(args.path);
                tabViewItem.Header = VideoTitle.Text;
                ClearVolumeControls();
                LoadMetadata(args.path);
                int trackCount = args.TrackPlayers.Count;
                var flyoutContent = MixedMediaPlayer.customMixedMediaPlayerControl?.TrackVolumeFlyoutContent;

                for (int i = 0; i < trackCount; i++)
                {
                    int trackIndex = i;
                    string trackNameText = GetTrackName(args.TrackPlayers[i], trackIndex);
                    double savedValue = GetSavedSliderValue(trackIndex);

                    Slider panelSlider = CreateVolumeSlider(trackIndex, new Thickness(0, 6, 0, 6), $"VolumeSlider_{trackIndex}", $"{trackNameText} volume");
                    panelSlider.Value = savedValue;
                    MixedMediaPlayer.SetVolume(trackIndex, savedValue);

                    var panelSliderChangedHandler = new RangeBaseValueChangedEventHandler(async (e, a) =>
                    {
                        if (syncingVolumeSliders)
                            return;

                        CachedSliderValues[trackIndex] = a.NewValue;
                        MixedMediaPlayer.SetVolume(trackIndex, a.NewValue);
                        SyncVolumeSlider(FlyoutVolumeSliders[trackIndex], a.NewValue);
                        await SaveMetadata();
                    });
                    panelSlider.ValueChanged += panelSliderChangedHandler;
                    VolumeSliderChangedHandlers.Add(panelSliderChangedHandler);
                    VolumeSliders.Add(panelSlider);

                    AddVolumeTrackToGrid(VolumeControlGrid, i, trackNameText, panelSlider);

                    if (flyoutContent != null)
                    {
                        Slider flyoutSlider = CreateVolumeSlider(trackIndex, new Thickness(0), $"VolumeFlyoutSlider_{trackIndex}", $"{trackNameText} volume");
                        flyoutSlider.Value = savedValue;
                        var flyoutSliderChangedHandler = new RangeBaseValueChangedEventHandler(async (e, a) =>
                        {
                            if (syncingVolumeSliders)
                                return;

                            CachedSliderValues[trackIndex] = a.NewValue;
                            MixedMediaPlayer.SetVolume(trackIndex, a.NewValue);
                            SyncVolumeSlider(panelSlider, a.NewValue);
                            await SaveMetadata();
                        });
                        flyoutSlider.ValueChanged += flyoutSliderChangedHandler;
                        FlyoutVolumeSliderChangedHandlers.Add(flyoutSliderChangedHandler);
                        FlyoutVolumeSliders.Add(flyoutSlider);
                        flyoutContent.Children.Add(CreateVolumeRow(trackNameText, flyoutSlider, FlyoutVolumeTrackLabelWidth));
                    }
                }
            });
        }
        private void ClearVolumeControls()
        {
            VolumeControlGrid.Children.Clear();
            VolumeControlGrid.RowDefinitions.Clear();
            for (int i = 0; i < VolumeSliders.Count && i < VolumeSliderChangedHandlers.Count; i++)
                VolumeSliders[i].ValueChanged -= VolumeSliderChangedHandlers[i];
            for (int i = 0; i < FlyoutVolumeSliders.Count && i < FlyoutVolumeSliderChangedHandlers.Count; i++)
                FlyoutVolumeSliders[i].ValueChanged -= FlyoutVolumeSliderChangedHandlers[i];

            VolumeSliders.Clear();
            VolumeSliderChangedHandlers.Clear();
            FlyoutVolumeSliders.Clear();
            FlyoutVolumeSliderChangedHandlers.Clear();
            MixedMediaPlayer.customMixedMediaPlayerControl?.TrackVolumeFlyoutContent?.Children.Clear();
        }

        private static string GetTrackName(MediaPlayer trackPlayer, int trackIndex)
        {
            try
            {
                string name = (trackPlayer.Source as MediaPlaybackItem).AudioTracks[trackIndex].Name;
                return string.IsNullOrEmpty(name) ? "Track" : name;
            }
            catch (Exception)
            {
                return $"Track {trackIndex + 1}";
            }
        }

        private static Slider CreateVolumeSlider(int trackIndex, Thickness margin, string automationId, string accessibleName)
        {
            var volumeSlider = new Slider
            {
                IsTabStop = true,
                UseSystemFocusVisuals = true,
                Margin = margin,
                TickFrequency = 5,
                TickPlacement = TickPlacement.None,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Maximum = 100,
                Minimum = 0,
            };
            AutomationProperties.SetAutomationId(volumeSlider, automationId);
            AutomationProperties.SetName(volumeSlider, accessibleName);
            return volumeSlider;
        }

        private static void AddVolumeTrackToGrid(Grid grid, int row, string trackNameText, Slider volumeSlider)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var trackName = new TextBlock
            {
                Text = trackNameText,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            ToolTipService.SetToolTip(trackName, trackNameText);

            Grid.SetRow(trackName, row);
            Grid.SetColumn(trackName, 0);
            Grid.SetRow(volumeSlider, row);
            Grid.SetColumn(volumeSlider, 1);
            grid.Children.Add(trackName);
            grid.Children.Add(volumeSlider);
        }

        private static Grid CreateVolumeRow(string trackNameText, Slider volumeSlider, double labelWidth)
        {
            var trackName = new TextBlock
            {
                Text = trackNameText,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            ToolTipService.SetToolTip(trackName, trackNameText);

            var row = new Grid
            {
                ColumnSpacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(trackName, 0);
            Grid.SetColumn(volumeSlider, 1);
            row.Children.Add(trackName);
            row.Children.Add(volumeSlider);
            return row;
        }

        private void SyncVolumeSlider(Slider targetSlider, double value)
        {
            if (targetSlider == null)
                return;

            syncingVolumeSliders = true;
            targetSlider.Value = value;
            syncingVolumeSliders = false;
        }

        public double[] GetVolumeLevels()
        {
            return VolumeSliders.Select(slider => slider.Value).ToArray();
        }
        public string GetCurrentPath()
        {
            return path;
        }

        public void Dispose()
        {
            if (_mixerToolsInTransport)
                RestoreMixerToolsToBottomPanel();

            keyboardPollCts?.Cancel();
            keyboardPollCts?.Dispose();
            keyboardPollCts = null;

            MixedMediaPlayer.MainMediaPlayer.MediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
            RatingSlider.UnregisterPropertyChangedCallback(RangeBase.ValueProperty, ratingValueChangedToken);
            PlaylistFilterTimeValue.ValueChanged -= PlaylistFilterTimeValue_ValueChanged;
            PlaylistFilterTimeUnit.SelectionChanged -= PlaylistFilterTimeUnit_SelectionChanged;
            PlaylistFilterChrono.Click -= PlaylistFilterMode_Click;
            PlaylistFilterRating.Click -= PlaylistFilterMode_Click;
            KeybindRecordingCapture.PreviewKeyDown -= KeybindRecordingCapture_PreviewKeyDown;
            CancelKeybindRecording();
            Loaded -= MixerPage_Loaded;
            MixedMediaPlayer.Loaded -= MixedMediaPlayer_Loaded;
            MixedMediaPlayer.FullScreenToggleRequested -= MixedMediaPlayer_FullScreenToggleRequested;
            MixedMediaPlayer.MediaLoaded -= MixedMediaPlayer_MediaLoaded;
            MixedMediaPlayer.MainMediaPlayer.MediaPlayer.CurrentStateChanged -= MediaPlayer_CurrentStateChanged;
            for (int i = 0; i < VolumeSliders.Count && i < VolumeSliderChangedHandlers.Count; i++)
                VolumeSliders[i].ValueChanged -= VolumeSliderChangedHandlers[i];
            for (int i = 0; i < FlyoutVolumeSliders.Count && i < FlyoutVolumeSliderChangedHandlers.Count; i++)
                FlyoutVolumeSliders[i].ValueChanged -= FlyoutVolumeSliderChangedHandlers[i];
            VolumeSliderChangedHandlers.Clear();
            FlyoutVolumeSliderChangedHandlers.Clear();
            MixedMediaPlayer.Dispose();
            MixedMediaPlayer = null;
        }
        private void OnRatingValueChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (suppressRatingSave)
                return;

            _ = SaveMetadata();
        }

        private void LoadMetadata(string path = null)
        {
            if (path == null)
                path = this.path;

            suppressRatingSave = true;
            if (AppState.TRACK_METADATA.ContainsKey(path))
            {
                CachedSliderValues = AppState.TRACK_METADATA[path].Sliders;
                RatingSlider.Value = AppState.TRACK_METADATA[path].Rating;
            }
            else
            {
                RatingSlider.Value = 0;
            }
            suppressRatingSave = false;
        }

        private async Task SaveMetadata(string path = null, double rating = -1)
        {
            if (rating < 0)
                rating = RatingSlider.Value;
            if (path == null)
                path = this.path;

            TrackMetadataStore.UpdateEntry(AppState.TRACK_METADATA, path, rating, CachedSliderValues);
            await TrackMetadataStore.PersistAsync(AppState.TRACK_METADATA, AppState.TrackMetadataJson);
        }
        public void PlayMedia()
        {
            MixedMediaPlayer.PlayAll();
        }
        public void PauseMedia()
        {
            MixedMediaPlayer.PauseAll();
        }

        private double GetSavedSliderValue(int index)
        {
            if (index < CachedSliderValues.Count)
                return CachedSliderValues[index];
            else
            {
                int difference = index - CachedSliderValues.Count + 1;
                if (difference > 0)
                {
                    CachedSliderValues.AddRange(Enumerable.Repeat(100.0, difference));
                }
                else if (difference < 0)
                {
                    CachedSliderValues.RemoveRange(index + 1, -difference);
                }
                return 100;
            }
        }
        internal void PlayPauseFromKeybind(KeyboardAccelerator? sender, KeyboardAcceleratorInvokedEventArgs? args)
        {
            if (MixedMediaPlayer.MainMediaPlayer.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                MixedMediaPlayer.PauseAll();
            else
                MixedMediaPlayer.PlayAll();
            args?.Handled = true;
        }

        internal void FastForwardFromKeybind(KeyboardAccelerator? sender, KeyboardAcceleratorInvokedEventArgs? args)
        {
            MixedMediaPlayer.FastForward(GetSkipOffsetMillis());
            args?.Handled = true;
        }

        internal void RewindFromKeybind(KeyboardAccelerator? sender, KeyboardAcceleratorInvokedEventArgs? args)
        {
            MixedMediaPlayer.Rewind(GetSkipOffsetMillis());
            args?.Handled = true;
        }

        private static int GetSkipOffsetMillis() =>
            LocalSettingsStore.GetSkipSeconds() * 1000;

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
                await MainWindow.Instance.ExportActiveTabAsync();
        }

        internal bool TryHandleKeybindRecording(KeyRoutedEventArgs args)
        {
            if (_recordingKeybindAction == null)
                return false;

            args.Handled = true;

            if (args.Key == VirtualKey.Escape)
            {
                CancelKeybindRecording();
                return true;
            }

            if (KeybindChordCapture.IsModifierKey(args.Key))
                return true;

            KeybindAction action = _recordingKeybindAction.Value;
            KeybindChord chord = KeybindChordCapture.CreateChord(args.Key);
            if (!KeybindStore.TrySet(action, chord, out string? validationError))
            {
                CancelKeybindRecording();
                _ = ShowKeybindRebindBlockedDialogAsync(validationError ?? "That shortcut can't be used.");
                return true;
            }

            if (_recordingKeybindShortcutBlock != null)
                _recordingKeybindShortcutBlock.Text = KeybindFormatter.Format(chord);
            ClearKeybindRecordingState();
            return true;
        }

        private async Task ShowKeybindRebindBlockedDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Shortcut not allowed",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = PageRootGrid.XamlRoot,
            };

            await dialog.ShowAsync();
        }

        private void RefreshKeybindList()
        {
            const int columns = 5;
            var items = KeybindStore.DisplayOrder;
            int rows = (items.Count + columns - 1) / columns;

            ClearKeybindRecordingState();
            KeybindListGrid.Children.Clear();
            KeybindListGrid.RowDefinitions.Clear();
            KeybindListGrid.ColumnDefinitions.Clear();

            for (int r = 0; r < rows; r++)
                KeybindListGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            for (int c = 0; c < columns; c++)
                KeybindListGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dividerBrush = Application.Current.Resources["DividerStrokeColorDefaultBrush"] as Microsoft.UI.Xaml.Media.Brush;
            var badgeBrush = Application.Current.Resources["ControlFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush;
            for (int i = 0; i < items.Count; i++)
            {
                (KeybindAction action, _) = items[i];
                int row = i / columns;
                int column = i % columns;

                var labelBlock = new TextBlock
                {
                    Text = KeybindStore.GetActionLabel(action),
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                };

                var shortcutBlock = new TextBlock
                {
                    Text = KeybindFormatter.Format(KeybindStore.Get(action)),
                    FontSize = 11,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var shortcutBadge = new Border
                {
                    Background = badgeBrush,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = shortcutBlock,
                };

                var cellContent = new StackPanel
                {
                    Spacing = 4,
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                };
                cellContent.Children.Add(labelBlock);
                cellContent.Children.Add(shortcutBadge);

                bool isLastRow = row == rows - 1;
                var cell = new Border
                {
                    Padding = new Thickness(10, 8, 10, 8),
                    BorderBrush = dividerBrush,
                    BorderThickness = new Thickness(column == 0 ? 0 : 1, row == 0 ? 0 : 1, 1, isLastRow ? 0 : 1),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Tag = action,
                    Child = cellContent,
                };
                AutomationProperties.SetAutomationId(cell, $"KeybindCell_{action}");
                ToolTipService.SetToolTip(cell, "Click to change shortcut");
                cell.PointerPressed += KeybindCell_PointerPressed;
                cell.PointerEntered += KeybindCell_PointerEntered;
                cell.PointerExited += KeybindCell_PointerExited;

                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                KeybindListGrid.Children.Add(cell);
            }
        }

        private void KeybindCell_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border cell || ReferenceEquals(cell, _recordingKeybindCell))
                return;

            cell.Background = Application.Current.Resources["ControlFillColorSecondaryBrush"] as Brush;
        }

        private void KeybindCell_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border cell || ReferenceEquals(cell, _recordingKeybindCell))
                return;

            cell.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        private void KeybindCell_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border clickedCell || clickedCell.Tag is not KeybindAction action)
                return;

            e.Handled = true;

            if (ReferenceEquals(clickedCell, _recordingKeybindCell))
            {
                CancelKeybindRecording();
                return;
            }

            CancelKeybindRecording();
            _recordingKeybindAction = action;
            _recordingKeybindCell = clickedCell;
            _recordingKeybindShortcutBlock = GetKeybindShortcutBlock(clickedCell);
            if (_recordingKeybindShortcutBlock != null)
                _recordingKeybindShortcutBlock.Text = "Press keys…";
            clickedCell.Background = Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush;
            KeybindRecordingCapture.IsHitTestVisible = true;
            KeybindRecordingCapture.Focus(FocusState.Programmatic);
        }

        private static TextBlock? GetKeybindShortcutBlock(Border cell)
        {
            if (cell.Child is not StackPanel stack || stack.Children.Count < 2)
                return null;
            if (stack.Children[1] is Border badge && badge.Child is TextBlock text)
                return text;
            return null;
        }

        private void CancelKeybindRecording()
        {
            if (_recordingKeybindAction == null)
                return;

            if (_recordingKeybindShortcutBlock != null)
                _recordingKeybindShortcutBlock.Text = KeybindFormatter.Format(KeybindStore.Get(_recordingKeybindAction.Value));
            ClearKeybindRecordingState();
        }

        private void ClearKeybindRecordingState()
        {
            if (_recordingKeybindCell != null)
                _recordingKeybindCell.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            KeybindRecordingCapture.IsHitTestVisible = false;
            KeybindRecordingCapture.Text = string.Empty;
            _recordingKeybindAction = null;
            _recordingKeybindShortcutBlock = null;
            _recordingKeybindCell = null;
        }
        static bool IsKeyDown(VirtualKey key)
        {
            return InputKeyboardSource
                .GetKeyStateForCurrentThread(key)
                .HasFlag(CoreVirtualKeyStates.Down);
        }

        public async Task<bool> ShowRootFolderManagerAsync()
        {
            RefreshRootFoldersDialog();
            await RootFoldersDialog.ShowAsync();
            return RootFolderStore.Folders.Count > 0;
        }

        void RefreshRootFoldersDialog()
        {
            RootFolderStore.EnsureLoaded();
            var folders = RootFolderStore.Folders.ToList();
            RootFolderList.ItemsSource = folders;
            bool hasFolders = folders.Count > 0;
            RootFolderList.Visibility = hasFolders ? Visibility.Visible : Visibility.Collapsed;
            RootFolderEmptyText.Visibility = hasFolders ? Visibility.Collapsed : Visibility.Visible;
            syncingSuppressRootFolderPromptCheckBox = true;
            SuppressRootFolderPromptCheckBox.IsChecked = MainWindow.RootFolderPromptSuppressed;
            syncingSuppressRootFolderPromptCheckBox = false;
        }

        private async void AddRootFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance == null)
                return;

            string folder = await MainWindow.Instance.PickRootFolderAsync();
            if (folder != null)
            {
                RootFolderStore.Add(folder);
                PrewarmPlaylistIndex(MixedMediaPlayer.PlaylistConfig, path);
            }
            RefreshRootFoldersDialog();
        }

        private void RemoveRootFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string folder })
            {
                RootFolderStore.Remove(folder);
                RefreshRootFoldersDialog();
            }
        }

        private void SuppressRootFolderPromptCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (syncingSuppressRootFolderPromptCheckBox)
                return;

            if (SuppressRootFolderPromptCheckBox.IsChecked is bool suppressed)
                MainWindow.RootFolderPromptSuppressed = suppressed;
        }
    }
}
