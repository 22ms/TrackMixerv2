using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace TrackMixerv2;

public static class SliderWheelBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SliderWheelBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not Slider slider)
            return;

        if (args.NewValue is bool enabled && enabled)
            slider.PointerWheelChanged += OnPointerWheelChanged;
        else
            slider.PointerWheelChanged -= OnPointerWheelChanged;
    }

    private static void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Slider slider)
            return;

        int wheelDelta = e.GetCurrentPoint(slider).Properties.MouseWheelDelta;
        if (wheelDelta == 0)
            return;

        double step = GetWheelStep(slider);
        double direction = wheelDelta > 0 ? 1 : -1;
        slider.Value = Math.Clamp(slider.Value + (direction * step), slider.Minimum, slider.Maximum);
        e.Handled = true;
    }

    internal static double GetWheelStep(Slider slider) =>
        SliderWheelRules.GetWheelStep(
            slider.SmallChange,
            slider.TickFrequency,
            LocalSettingsStore.GetSliderWheelSpeed());
}
