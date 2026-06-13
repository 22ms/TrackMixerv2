using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace TrackMixerv2
{
    public sealed class MixedMediaPlayerControl : MediaTransportControls
    {
        private const double SliderThumbWidth = 24;

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
        public Slider PlaybackRateSlider;

        private Canvas _playbackRateTickLabelsCanvas;
        private StackPanel _speedSelectFlyoutContent;
        private Thumb _playbackRateThumb;
        private ToolTip _playbackRateThumbToolTip;
        private readonly List<double> _transportRates = new List<double>();
        private bool _syncingPlaybackRateSlider;
        private bool _playbackRateSliderChromeHooked;

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
            _speedSelectFlyoutContent = GetTemplateChild("SpeedSelectFlyoutContent") as StackPanel;
            PlaybackRateSlider = GetTemplateChild("PlaybackRateSlider") as Slider;
            _playbackRateTickLabelsCanvas = GetTemplateChild("PlaybackRateTickLabelsCanvas") as Canvas;
            FullScreenButton = GetTemplateChild("FullScreenButton") as AppBarButton;
            FullScreenSymbol = GetTemplateChild("FullScreenSymbol") as FontIcon;
            AutoplayForwardOption = GetTemplateChild("AutoplayForwardOption") as RadioMenuFlyoutItem;
            AutoplayBackwardOption = GetTemplateChild("AutoplayBackwardOption") as RadioMenuFlyoutItem;
            AutoplayOffOption = GetTemplateChild("AutoplayOffOption") as RadioMenuFlyoutItem;
            AutoplaySmallIcon = GetTemplateChild("AutoplaySmallIcon") as FontIcon;

            EnsurePlaybackRateSliderChrome();
            if (_playbackRateTickLabelsCanvas != null)
                _playbackRateTickLabelsCanvas.SizeChanged += (_, __) => LayoutPlaybackRateTickLabels();

            EnsureCustomPlaybackRateFlyout();
        }

        public void EnsureCustomPlaybackRateFlyout()
        {
            if (SpeedSelectButton == null || PlaybackRateSlider == null)
                return;

            if (_transportRates.Count == 0)
                RebuildPlaybackRateFlyout();
            else
                SpeedSelectButton.IsEnabled = true;
        }

        public void RebuildPlaybackRateFlyout(double? currentRate = null)
        {
            if (SpeedSelectButton == null || PlaybackRateSlider == null)
                return;

            _transportRates.Clear();
            _transportRates.AddRange(PlaybackRates.All);

            int lastIndex = Math.Max(0, _transportRates.Count - 1);
            PlaybackRateSlider.Minimum = 0;
            PlaybackRateSlider.Maximum = lastIndex;
            PlaybackRateSlider.StepFrequency = 1;
            PlaybackRateSlider.SmallChange = 1;
            PlaybackRateSlider.LargeChange = 1;
            PlaybackRateSlider.TickFrequency = 1;
            PlaybackRateSlider.TickPlacement = TickPlacement.BottomRight;

            RebuildPlaybackRateTickLabels();

            if (_speedSelectFlyoutContent != null)
                _speedSelectFlyoutContent.MinWidth = Math.Max(280, _transportRates.Count * 32);

            SpeedSelectButton.IsEnabled = _transportRates.Count > 0;

            _syncingPlaybackRateSlider = true;
            try
            {
                if (currentRate.HasValue)
                    PlaybackRateSlider.Value = IndexOfRate(PlaybackRates.SnapToNearestTransportRate(currentRate.Value));
                else if (_transportRates.Count > 0)
                    PlaybackRateSlider.Value = IndexOfRate(1);
            }
            finally
            {
                _syncingPlaybackRateSlider = false;
            }

            UpdatePlaybackRateThumbToolTip();
            LayoutPlaybackRateTickLabels();
        }

        public void SetPlaybackRateSelection(double rate)
        {
            if (PlaybackRateSlider == null || _transportRates.Count == 0)
                return;

            _syncingPlaybackRateSlider = true;
            PlaybackRateSlider.Value = IndexOfRate(rate);
            _syncingPlaybackRateSlider = false;
            UpdatePlaybackRateThumbToolTip();
        }

        public bool TryGetSelectedPlaybackRate(out double rate)
        {
            if (PlaybackRateSlider == null || _transportRates.Count == 0)
            {
                rate = 1;
                return false;
            }

            int index = (int)Math.Round(PlaybackRateSlider.Value);
            index = Math.Clamp(index, 0, _transportRates.Count - 1);
            rate = _transportRates[index];
            return true;
        }

        public bool IsSyncingPlaybackRateSlider => _syncingPlaybackRateSlider;

        private void EnsurePlaybackRateSliderChrome()
        {
            if (PlaybackRateSlider == null || _playbackRateSliderChromeHooked)
                return;

            PlaybackRateSlider.Loaded += PlaybackRateSlider_Loaded;
            PlaybackRateSlider.SizeChanged += PlaybackRateSlider_SizeChanged;
            PlaybackRateSlider.PointerPressed += PlaybackRateSlider_PointerPressed;
            PlaybackRateSlider.PointerReleased += PlaybackRateSlider_PointerReleased;
            PlaybackRateSlider.PointerCanceled += PlaybackRateSlider_PointerCanceled;
            PlaybackRateSlider.RegisterPropertyChangedCallback(
                RangeBase.ValueProperty,
                (_, __) => OnPlaybackRateSliderValueChanged());
            _playbackRateSliderChromeHooked = true;
        }

        private void PlaybackRateSlider_Loaded(object sender, RoutedEventArgs e)
        {
            EnsurePlaybackRateThumbToolTip();
            UpdatePlaybackRateThumbToolTip();
            LayoutPlaybackRateTickLabels();
        }

        private void PlaybackRateSlider_SizeChanged(object sender, SizeChangedEventArgs e) =>
            LayoutPlaybackRateTickLabels();

        private void PlaybackRateSlider_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
            OpenPlaybackRateThumbToolTip();

        private void PlaybackRateSlider_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
            ClosePlaybackRateThumbToolTip();

        private void PlaybackRateSlider_PointerCanceled(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
            ClosePlaybackRateThumbToolTip();

        private void OnPlaybackRateSliderValueChanged()
        {
            if (_syncingPlaybackRateSlider)
                return;

            UpdatePlaybackRateThumbToolTip();
        }

        private void EnsurePlaybackRateThumbToolTip()
        {
            _playbackRateThumb ??= FindDescendant<Thumb>(PlaybackRateSlider);
            if (_playbackRateThumb == null)
                return;

            _playbackRateThumbToolTip ??= new ToolTip();
            ToolTipService.SetToolTip(_playbackRateThumb, _playbackRateThumbToolTip);
            ToolTipService.SetPlacement(_playbackRateThumb, PlacementMode.Top);
        }

        private void OpenPlaybackRateThumbToolTip()
        {
            UpdatePlaybackRateThumbToolTip();
            if (_playbackRateThumbToolTip != null)
                _playbackRateThumbToolTip.IsOpen = true;
        }

        private void ClosePlaybackRateThumbToolTip()
        {
            if (_playbackRateThumbToolTip != null)
                _playbackRateThumbToolTip.IsOpen = false;
        }

        private void UpdatePlaybackRateThumbToolTip()
        {
            EnsurePlaybackRateThumbToolTip();
            if (_playbackRateThumbToolTip == null || !TryGetSelectedPlaybackRate(out double rate))
                return;

            _playbackRateThumbToolTip.Content = CreatePlaybackRateLabel(rate, 12);
        }

        private void RebuildPlaybackRateTickLabels()
        {
            if (_playbackRateTickLabelsCanvas == null)
                return;

            _playbackRateTickLabelsCanvas.Children.Clear();

            for (int i = 0; i < _transportRates.Count; i++)
            {
                double rate = _transportRates[i];
                var label = CreatePlaybackRateLabel(rate, 10);
                label.Tag = i;
                _playbackRateTickLabelsCanvas.Children.Add(label);
            }
        }

        private void LayoutPlaybackRateTickLabels()
        {
            if (_playbackRateTickLabelsCanvas == null || _transportRates.Count == 0)
                return;

            double width = _playbackRateTickLabelsCanvas.ActualWidth;
            if (width <= 0)
                return;

            double trackLength = Math.Max(0, width - SliderThumbWidth);
            double thumbInset = SliderThumbWidth / 2;
            int lastIndex = Math.Max(1, _transportRates.Count - 1);
            double maxHeight = 0;

            foreach (UIElement child in _playbackRateTickLabelsCanvas.Children)
            {
                if (child is not TextBlock label || label.Tag is not int index)
                    continue;

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double fraction = (double)index / lastIndex;
                double centerX = thumbInset + (fraction * trackLength);
                Canvas.SetLeft(label, centerX - (label.DesiredSize.Width / 2));
                Canvas.SetTop(label, 0);
                maxHeight = Math.Max(maxHeight, label.DesiredSize.Height);
            }

            if (maxHeight > 0)
                _playbackRateTickLabelsCanvas.Height = maxHeight;
        }

        private TextBlock CreatePlaybackRateLabel(double rate, double fontSize)
        {
            bool isNormalSpeed = Math.Abs(rate - 1) < 0.001;
            return new TextBlock
            {
                Text = FormatPlaybackRate(rate),
                FontSize = fontSize,
                TextWrapping = TextWrapping.NoWrap,
                FontWeight = isNormalSpeed ? FontWeights.Bold : FontWeights.Normal,
                Opacity = isNormalSpeed ? 1 : 0.85,
            };
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
                return null;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                    return match;

                var descendant = FindDescendant<T>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }

        private int IndexOfRate(double rate)
        {
            double snapped = PlaybackRates.SnapToNearestTransportRate(rate);
            for (int i = 0; i < _transportRates.Count; i++)
            {
                if (Math.Abs(_transportRates[i] - snapped) < 0.001)
                    return i;
            }

            for (int i = 0; i < _transportRates.Count; i++)
            {
                if (Math.Abs(_transportRates[i] - 1) < 0.001)
                    return i;
            }

            return 0;
        }

        private static string FormatPlaybackRate(double rate) => PlaybackRates.Format(rate);
    }
}
