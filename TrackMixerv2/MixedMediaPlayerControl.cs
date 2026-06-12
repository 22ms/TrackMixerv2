using Microsoft.UI.Xaml.Controls;

namespace TrackMixerv2
{
    public sealed class MixedMediaPlayerControl : MediaTransportControls
    {
        public Slider ProgressSlider;
        public Button NextTrackButton, PreviousTrackButton;
        public AppBarButton FullScreenButton;
        public FontIcon FullScreenSymbol;
        public MenuFlyoutItem AutoplayForwardOption, AutoplayBackwardOption, AutoplayOffOption;
        public FontIcon AutoplaySmallIcon;

        public MixedMediaPlayerControl()
        {
            this.DefaultStyleKey = typeof(MixedMediaPlayerControl);
        }

        protected override void OnApplyTemplate()
        {
            ProgressSlider = GetTemplateChild("ProgressSlider") as Slider;
            NextTrackButton = GetTemplateChild("NextTrackButton") as Button;
            PreviousTrackButton = GetTemplateChild("PreviousTrackButton") as Button;
            FullScreenButton = GetTemplateChild("FullScreenButton") as AppBarButton;
            FullScreenSymbol = GetTemplateChild("FullScreenSymbol") as FontIcon;
            AutoplayForwardOption = GetTemplateChild("AutoplayForwardOption") as MenuFlyoutItem;
            AutoplayBackwardOption = GetTemplateChild("AutoplayBackwardOption") as MenuFlyoutItem;
            AutoplayOffOption = GetTemplateChild("AutoplayOffOption") as MenuFlyoutItem;
            AutoplaySmallIcon = GetTemplateChild("AutoplaySmallIcon") as FontIcon;
            base.OnApplyTemplate();
        }
    }
}
