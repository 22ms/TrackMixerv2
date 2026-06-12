using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace TrackMixerv2
{
    internal static class FlyoutMenuHelper
    {
        public static void SelectByTag(IEnumerable<RadioMenuFlyoutItem> items, string tag)
        {
            foreach (var item in items)
                item.IsChecked = FlyoutSelection.TagMatches(item.Tag, tag);
        }

        public static void SelectByPlaybackRate(IEnumerable<RadioMenuFlyoutItem> items, double rate)
        {
            foreach (var item in items)
                item.IsChecked = FlyoutSelection.PlaybackRateMatches(item.Tag, rate);
        }
    }
}
