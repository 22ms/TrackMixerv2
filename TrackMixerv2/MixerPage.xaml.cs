using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Diagnostics;
using System.Linq;
using Windows.Media.Playback;
using static TrackMixerv2.MixedMediaPlayer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TrackMixerv2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MixerPage : Page
    {

        private bool _autoPlay;
        private DispatcherQueue dispatcherQueue;
        private TabViewItem tabViewItem;

        public MixerPage()
        {
            this.InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            this.MixedMediaPlayer.Loaded += MixedMediaPlayer_Loaded;
        }

        public MixerPage(TabViewItem tabViewItem)
        {
            this.InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            this.tabViewItem = tabViewItem;
            this.MixedMediaPlayer.Loaded += MixedMediaPlayer_Loaded;
        }

        private void MixedMediaPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            // C:\Users\Leand\Videos\Rocket League\Rocket League 2023.09.10 - 21.15.15.11.DVR.mp4 - 2
            // E:\Videos\OBS\Replay 2022-03-30 22-04-17 _new.mp4 - 4
            if(tabViewItem == null)
                tabViewItem = Parent as TabViewItem;
            string path = @"C:\Users\Leand\Videos\PokerStars VR\PokerStars VR 2023.01.21 - 22.14.56.14.mp4";
            MixedMediaPlayer.OpenMediaAsync(path);
            VideoTitle.Text = GetTitleFromPath(path);
            tabViewItem.Header = VideoTitle.Text;
            MixedMediaPlayer.MediaLoaded += MixedMediaPlayer_MediaLoaded;
        }

        private void MixedMediaPlayer_MediaLoaded(object sender, MediaLoadedEventArgs args)
        {
            // create volume sliders
            tabViewItem.IconSource = new SymbolIconSource() { Symbol = Symbol.Pictures };
            dispatcherQueue.TryEnqueue(() =>
            {
                VolumeControlGrid.Children.Clear();
                VolumeControlGrid.ColumnDefinitions.Clear();
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
                    volumeSlider.Value = 100;
                    MixedMediaPlayer.SetVolume(trackIndex, 100);
                    volumeSlider.ValueChanged += (e, a) =>
                    {
                        MixedMediaPlayer.SetVolume(trackIndex, a.NewValue);
                    };
                    
                    TextBlock trackName = new TextBlock();
                    trackName.Text = (args.TrackPlayers[i].Source as MediaPlaybackItem).AudioTracks[i].Name;
                    trackName.HorizontalAlignment = HorizontalAlignment.Center;
                    trackName.TextAlignment = TextAlignment.Center;

                    ColumnDefinition column = new ColumnDefinition();
                    column.Width = new GridLength(1, GridUnitType.Star);
                    VolumeControlGrid.ColumnDefinitions.Add(column);

                    StackPanel stackPanel = new StackPanel();
                    stackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    stackPanel.Children.Add(volumeSlider);
                    stackPanel.Children.Add(trackName);
                    Grid.SetColumn(stackPanel, i);
                    VolumeControlGrid.Children.Add(stackPanel);
                }
            });
        }

        private void AutoPlay_Checked(object sender, RoutedEventArgs e)
        {
            _autoPlay = true;
        }

        private void AutoPlay_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoPlay = false;
        }

        private string GetTitleFromPath(string path)
        {
            return path.Split("\\").Reverse().ToList()[0];
        }
    }
}
