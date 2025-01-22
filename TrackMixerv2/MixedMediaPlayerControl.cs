using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TrackMixerv2
{
    public sealed class MixedMediaPlayerControl : MediaTransportControls
    {
        public Slider ProgressSlider;
        public Button NextTrackButton, PreviousTrackButton;
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
            AutoplayForwardOption = GetTemplateChild("AutoplayForwardOption") as MenuFlyoutItem;
            AutoplayBackwardOption = GetTemplateChild("AutoplayBackwardOption") as MenuFlyoutItem;
            AutoplayOffOption = GetTemplateChild("AutoplayOffOption") as MenuFlyoutItem;
            AutoplaySmallIcon = GetTemplateChild("AutoplaySmallIcon") as FontIcon;
            base.OnApplyTemplate();
        }
    }
}
