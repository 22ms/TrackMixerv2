using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace TrackMixerv2;

public enum SliderStepSetting
{
    Fixed,
    Volume,
    Rating,
    Progress,
}

public static class SliderWheelBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SliderWheelBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty StepSettingProperty =
        DependencyProperty.RegisterAttached(
            "StepSetting",
            typeof(SliderStepSetting),
            typeof(SliderWheelBehavior),
            new PropertyMetadata(SliderStepSetting.Fixed));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static SliderStepSetting GetStepSetting(DependencyObject obj) =>
        (SliderStepSetting)obj.GetValue(StepSettingProperty);

    public static void SetStepSetting(DependencyObject obj, SliderStepSetting value) =>
        obj.SetValue(StepSettingProperty, value);

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
            GetStepSize(GetStepSetting(slider)));

    private static int GetStepSize(SliderStepSetting setting) => setting switch
    {
        SliderStepSetting.Volume => LocalSettingsStore.GetSliderStepSize(LocalSettingsStore.Keys.VolumeSliderStepSize),
        SliderStepSetting.Rating => LocalSettingsStore.GetSliderStepSize(LocalSettingsStore.Keys.RatingSliderStepSize),
        SliderStepSetting.Progress => LocalSettingsStore.GetSliderStepSize(LocalSettingsStore.Keys.ProgressSliderStepSize),
        _ => 1,
    };
}
