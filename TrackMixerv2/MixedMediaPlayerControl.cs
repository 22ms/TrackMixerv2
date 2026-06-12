using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace TrackMixerv2
{
    public sealed class MixedMediaPlayerControl : MediaTransportControls
    {
        private static Style _compactSelectionFlyoutRadioItemStyle;

        private static readonly double[] PlaybackRates =
        {
            0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.5, 3, 3.5, 4, 4.5, 5, 6, 7, 8
        };

        public Slider ProgressSlider;
        public Button NextTrackButton, PreviousTrackButton;
        public StackPanel TrackVolumeFlyoutContent;
        public StackPanel FullscreenMixerToolsHost;
        public StackPanel FullscreenMixerToolsLeadingHost;
        public StackPanel FullscreenMixerToolsTrailingHost;
        public AppBarButton SpeedSelectButton;
        public AppBarButton FullScreenButton;
        public FontIcon FullScreenSymbol;
        public RadioMenuFlyoutItem AutoplayForwardOption, AutoplayBackwardOption, AutoplayOffOption;
        public FontIcon AutoplaySmallIcon;
        public IReadOnlyList<RadioMenuFlyoutItem> PlaybackRateOptions => _playbackRateOptions;

        private readonly List<RadioMenuFlyoutItem> _playbackRateOptions = new List<RadioMenuFlyoutItem>();
        private MenuFlyout _speedSelectMenuFlyout;

        public MixedMediaPlayerControl()
        {
            this.DefaultStyleKey = typeof(MixedMediaPlayerControl);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            ProgressSlider = GetTemplateChild("ProgressSlider") as Slider;
            NextTrackButton = GetTemplateChild("NextTrackButton") as Button;
            PreviousTrackButton = GetTemplateChild("PreviousTrackButton") as Button;
            TrackVolumeFlyoutContent = GetTemplateChild("TrackVolumeFlyoutContent") as StackPanel;
            FullscreenMixerToolsHost = GetTemplateChild("FullscreenMixerToolsHost") as StackPanel;
            FullscreenMixerToolsLeadingHost = GetTemplateChild("FullscreenMixerToolsLeadingHost") as StackPanel;
            FullscreenMixerToolsTrailingHost = GetTemplateChild("FullscreenMixerToolsTrailingHost") as StackPanel;
            SpeedSelectButton = GetTemplateChild("SpeedSelectButton") as AppBarButton;
            _speedSelectMenuFlyout = GetTemplateChild("SpeedSelectMenuFlyout") as MenuFlyout;
            FullScreenButton = GetTemplateChild("FullScreenButton") as AppBarButton;
            FullScreenSymbol = GetTemplateChild("FullScreenSymbol") as FontIcon;
            AutoplayForwardOption = GetTemplateChild("AutoplayForwardOption") as RadioMenuFlyoutItem;
            AutoplayBackwardOption = GetTemplateChild("AutoplayBackwardOption") as RadioMenuFlyoutItem;
            AutoplayOffOption = GetTemplateChild("AutoplayOffOption") as RadioMenuFlyoutItem;
            AutoplaySmallIcon = GetTemplateChild("AutoplaySmallIcon") as FontIcon;

            EnsureCustomPlaybackRateFlyout();
        }

        public void EnsureCustomPlaybackRateFlyout()
        {
            if (SpeedSelectButton == null || _speedSelectMenuFlyout == null)
                return;

            if (_playbackRateOptions.Count == 0)
            {
                var itemStyle = _compactSelectionFlyoutRadioItemStyle
                    ??= Application.Current.Resources["CompactSelectionFlyoutRadioItemStyle"] as Style;

                foreach (double rate in PlaybackRates)
                {
                    var item = new RadioMenuFlyoutItem
                    {
                        GroupName = "PlaybackRate",
                        Tag = rate,
                        Text = FormatPlaybackRate(rate),
                        IsChecked = Math.Abs(rate - 1) < 0.001,
                        Style = itemStyle
                    };
                    _playbackRateOptions.Add(item);
                    _speedSelectMenuFlyout.Items.Add(item);
                }
            }

            SpeedSelectButton.IsEnabled = true;
        }

        public void SetPlaybackRateSelection(double rate)
        {
            FlyoutMenuHelper.SelectByPlaybackRate(_playbackRateOptions, rate);
        }

        private static string FormatPlaybackRate(double rate)
        {
            if (Math.Abs(rate - 1) < 0.001)
                return "1×";

            return rate % 1 == 0 ? $"{rate:0}×" : $"{rate:0.##}×";
        }
    }
}
