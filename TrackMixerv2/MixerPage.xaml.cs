using Microsoft.UI.Dispatching;
using Microsoft.VisualBasic.FileIO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Storage;
using static TrackMixerv2.MainWindow;
using static TrackMixerv2.MixedMediaPlayer;
using static TrackMixerv2.PlaylistHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TrackMixerv2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MixerPage : Page
    {
        DispatcherQueue dispatcherQueue;
        TabViewItem tabViewItem;
        bool initialLoaded = false;
        RangeBaseValueChangedEventHandler VolumeSliderChangedHandler;
        List<Slider> VolumeSliders = new List<Slider>();
        List<double> CachedSliderValues = new List<double>();
        string path;
        public MixerPage(string path)
        {
            this.InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            this.path = path;
            MixedMediaPlayer.Loaded += MixedMediaPlayer_Loaded;
            MixedMediaPlayer.MainMediaPlayer.MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            RatingSlider.Loaded += RatingSlider_Loaded;
            PlaylistFilterTimeValue.ValueChanged += PlaylistFilterTimeValue_ValueChanged;
            PlaylistFilterTimeUnit.SelectionChanged += PlaylistFilterTimeUnit_SelectionChanged;
            PlaylistFilterChrono.Click += PlaylistFilterMode_Click;
            PlaylistFilterRating.Click += PlaylistFilterMode_Click;
            PlaylistSubfolderToggle.Click += PlaylistSubfolderToggle_Click;
        }
        private async void Preferences_Click(object sender, RoutedEventArgs e)
        {
            var result = await PreferencesDialog.ShowAsync();

            // When the "save" button is pressed, save the settings
            if (result == ContentDialogResult.Primary)
            {
                ApplicationData.Current.LocalSettings.Values["DragAndDropOnNewTab"] = DragAndDropCheckBox.IsChecked ?? false;
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
            DeleteConfirmationFlyout.Hide();
        }

        private void CancelDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteConfirmationFlyout.Hide();
        }


        private void PlaylistSubfolderToggle_Click(object sender, RoutedEventArgs e)
        {
            MixedMediaPlayer.PlaylistConfig.SubfolderOnly = PlaylistSubfolderToggle.IsChecked.GetValueOrDefault();
        }

        private void PlaylistFilterTimeUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MixedMediaPlayer.PlaylistConfig.TimeSpan = TimeSpanFromUnitValue((TimeUnit)PlaylistFilterTimeUnit.SelectedIndex,PlaylistFilterTimeValue.Value );
        }

        private void PlaylistFilterTimeValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            MixedMediaPlayer.PlaylistConfig.TimeSpan = TimeSpanFromUnitValue((TimeUnit)PlaylistFilterTimeUnit.SelectedIndex, PlaylistFilterTimeValue.Value);
        }

        private void PlaylistFilterMode_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            if (item == null) return;
            switch (item.Tag)
            {
                case "chrono":
                    PlaylistFilterTimeValue.Visibility = Visibility.Collapsed;
                    PlaylistFilterTimeUnit.Visibility = Visibility.Collapsed;
                    PlaylistFilterSelectionIcon.Glyph = (PlaylistFilterChrono.Icon as FontIcon).Glyph;
                    MixedMediaPlayer.PlaylistConfig.PlaylistMode = PlaylistMode.Chrono;
                    break;
                case "rating":
                    PlaylistFilterTimeValue.Visibility = Visibility.Visible;
                    PlaylistFilterTimeUnit.Visibility = Visibility.Visible;
                    PlaylistFilterSelectionIcon.Glyph = (PlaylistFilterRating.Icon as FontIcon).Glyph;

                    MixedMediaPlayer.PlaylistConfig.PlaylistMode = PlaylistMode.Rating;
                    MixedMediaPlayer.PlaylistConfig.TimeSpan = TimeSpanFromUnitValue((TimeUnit)PlaylistFilterTimeUnit.SelectedIndex, PlaylistFilterTimeValue.Value);
                    break;
            }
        }

        public void OpenNewMedia (string path)
        {
            this.path = path;
            MixedMediaPlayer.Dispose();
            MixedMediaPlayer.OpenMediaAsync(path);
        }

        public void PlayMedia()
        {
            MixedMediaPlayer.PlayAll();
        }

        public void PauseMedia()
        {
            MixedMediaPlayer.PauseAll();
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Debug.WriteLine(args.Error);
        }

        private void MixedMediaPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                if (initialLoaded) return;
                if (tabViewItem == null)
                    tabViewItem = Parent as TabViewItem;
                MixedMediaPlayer.OpenMediaAsync(path);
                MixedMediaPlayer.MediaLoaded += MixedMediaPlayer_MediaLoaded;
                MixedMediaPlayer.MainMediaPlayer.MediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;
                
                // Load settings
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("DragAndDropOnNewTab"))
                {
                    DragAndDropCheckBox.IsChecked = (bool)ApplicationData.Current.LocalSettings.Values["DragAndDropOnNewTab"];
                }
                else
                {
                    DragAndDropCheckBox.IsChecked = true;
                }
                
                initialLoaded = true;
            });
        }

        private void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                if (sender.CurrentState == MediaPlayerState.Playing)
                    tabViewItem.IconSource = new SymbolIconSource() { Symbol = Symbol.Volume }; // TODO ini only once but yeah performance egal
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
                VideoTitle.Text = Helper.GetTitleFromPath(args.path);
                tabViewItem.Header = VideoTitle.Text;
                VolumeControlGrid.Children.Clear();
                VolumeControlGrid.ColumnDefinitions.Clear();
                VolumeSliders.Clear();
                LoadMetadata(args.path);
                for (int i = 0; i < args.TrackPlayers.Count; i++)
                {
                    int trackIndex = i;
                    Slider volumeSlider = new Slider();
                    volumeSlider.Height = 100;
                    volumeSlider.TickFrequency = 5;
                    volumeSlider.TickPlacement = TickPlacement.Outside;
                    volumeSlider.Orientation = Orientation.Vertical;
                    volumeSlider.HorizontalAlignment = HorizontalAlignment.Center;
                    volumeSlider.Maximum = 100;
                    volumeSlider.Minimum = 0;
                    volumeSlider.Value = GetSavedSliderValue(trackIndex);

                    MixedMediaPlayer.SetVolume(trackIndex, volumeSlider.Value);
                    VolumeSliderChangedHandler = new RangeBaseValueChangedEventHandler(async (e, a) =>
                    {
                        CachedSliderValues[trackIndex] = a.NewValue;
                        MixedMediaPlayer.SetVolume(trackIndex, a.NewValue);
                        await SaveMetadata();
                    });
                    volumeSlider.ValueChanged += VolumeSliderChangedHandler;
                    VolumeSliders.Add(volumeSlider);

                    TextBlock trackName = new TextBlock();
                    try
                    {
                        trackName.Text = (args.TrackPlayers[i].Source as MediaPlaybackItem).AudioTracks[i].Name;
                    }
                    catch(Exception)
                    {
                        trackName.Text = i.ToString();
                    }
                    trackName.HorizontalAlignment = HorizontalAlignment.Center;
                    trackName.TextAlignment = TextAlignment.Center;

                    ColumnDefinition column = new ColumnDefinition();
                    column.Width = new GridLength(1, GridUnitType.Auto);
                    VolumeControlGrid.ColumnDefinitions.Add(column);

                    StackPanel stackPanel = new StackPanel();
                    Style stackPanelStyle = (Style)Application.Current.Resources["VolumeControlStackPanelStyle"];
                    stackPanel.Style = stackPanelStyle;
                    stackPanel.Children.Add(volumeSlider);
                    stackPanel.Children.Add(trackName);
                    Grid.SetColumn(stackPanel, i);
                    VolumeControlGrid.Children.Add(stackPanel);
                }
            });
        }

        public void Dispose()
        {
            MixedMediaPlayer.MainMediaPlayer.MediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
            RatingSlider.Loaded -= RatingSlider_Loaded;
            RatingSlider.ValueChanged -= RatingSlider_ValueChanged;
            PlaylistFilterTimeValue.ValueChanged -= PlaylistFilterTimeValue_ValueChanged;
            PlaylistFilterTimeUnit.SelectionChanged -= PlaylistFilterTimeUnit_SelectionChanged;
            PlaylistFilterChrono.Click -= PlaylistFilterMode_Click;
            PlaylistFilterRating.Click -= PlaylistFilterMode_Click;
            MixedMediaPlayer.Loaded -= MixedMediaPlayer_Loaded;
            MixedMediaPlayer.MediaLoaded -= MixedMediaPlayer_MediaLoaded;
            MixedMediaPlayer.MainMediaPlayer.MediaPlayer.CurrentStateChanged -= MediaPlayer_CurrentStateChanged;
            foreach (var volumeSlider in VolumeSliders)
            {
                volumeSlider.ValueChanged -= VolumeSliderChangedHandler;
            }
            MixedMediaPlayer.Dispose();
            MixedMediaPlayer = null;
        }
        private void RatingSlider_Loaded(object sender, RoutedEventArgs e)
        {
        }
        private async void RatingSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            await SaveMetadata(rating: e.NewValue);
        }

        private void LoadMetadata(string path = null)
        {
            if (path == null)
            {
                path = this.path;
            }
            RatingSlider.ValueChanged -= RatingSlider_ValueChanged; // workaround for bug causing the saving to occur too early
            if (TRACK_METADATA.ContainsKey(path))
            {
                CachedSliderValues = TRACK_METADATA[path].Sliders;
                RatingSlider.Value = TRACK_METADATA[path].Rating;
            }
            else
            {
                RatingSlider.Value = 0;
            }
            RatingSlider.ValueChanged += RatingSlider_ValueChanged;
        }

        private async Task SaveMetadata(string path = null, double rating = -1)
        {
            if (rating < 0)
                rating = RatingSlider.Value;
            if (path == null)
                path = this.path;
            if (TRACK_METADATA.ContainsKey(path))
            {
                TRACK_METADATA[path].Rating = RatingSlider.Value;
                TRACK_METADATA[path].Sliders = CachedSliderValues;
            }
            else
            {
                TRACK_METADATA.Add(path, new TrackMetadata(rating, CachedSliderValues));
            }
            await File.WriteAllTextAsync(TRACK_METADATA_JSON, JsonConvert.SerializeObject(TRACK_METADATA));
            TRACK_METADATA = JsonConvert.DeserializeObject<Dictionary<string, TrackMetadata>>(await File.ReadAllTextAsync(TRACK_METADATA_JSON)); // workaround, not sure why i need that line
        }

        private double GetSavedSliderValue(int index)
        {
            if(index < CachedSliderValues.Count)
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
    }
}
