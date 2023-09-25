using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TrackMixerv2
{
    public sealed class MixedMediaPlayerControl : MediaTransportControls
    {
        public Button NextTrackButton, PreviousTrackButton;
        public MenuFlyoutItem AutoplayForwardOption, AutoplayBackwardOption, AutoplayOffOption;
        public FontIcon AutoplaySmallIcon;

        public MixedMediaPlayerControl()
        {
            this.DefaultStyleKey = typeof(MixedMediaPlayerControl);
        }

        protected override void OnApplyTemplate()
        {
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
