using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
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

        public MixerPage()
        {
            this.InitializeComponent();
            this.MixedMediaPlayer.Loaded += MixedMediaPlayer_Loaded;
        }

        private void MixedMediaPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            MixedMediaPlayer.OpenMediaAsync(@"C:\Users\Leand\Videos\Rocket League\Rocket League 2023.09.10 - 21.15.15.11.DVR.mp4");
            VideoTitle.Text = GetTitleFromPath(@"C:\Users\Leand\Videos\Rocket League\Rocket League 2023.09.10 - 21.15.15.11.DVR.mp4");
            MixedMediaPlayer.MediaLoaded += MixedMediaPlayer_MediaLoaded;
        }

        private void MixedMediaPlayer_MediaLoaded(object sender, MediaLoadedEventArgs args)
        {
            // create volume sliders
            VolumeControlGrid.Children.Clear();
            for (int i = 0; i < args.AudioTrackCount; i++)
            {
                int trackIndex = i;
                Slider volumeSlider = new Slider();
                volumeSlider.Width = 100;
                volumeSlider.Height = 100;
                volumeSlider.TickFrequency = 10;
                volumeSlider.TickPlacement = TickPlacement.Outside;
                volumeSlider.Maximum = 100;
                volumeSlider.Minimum = 0;
                volumeSlider.ValueChanged += (e, a) => 
                {
                    MixedMediaPlayer.SetVolume(trackIndex, a.NewValue);
                };
                Grid.SetColumn(volumeSlider, i);
                ColumnDefinition column = new ColumnDefinition();
                column.Width = new GridLength(1, GridUnitType.Star);
                VolumeControlGrid.ColumnDefinitions.Add(column);
                VolumeControlGrid.Children.Add(volumeSlider);
            }
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
