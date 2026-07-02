using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace TrackMixerv2;

public static class NumberBoxHelper
{
    // Assigning Value alone does not repaint the text when the new value equals the
    // stored one, and a box whose inner TextBox had keyboard focus when its dialog
    // closed renders empty on the next open until its text is invalidated. Assign
    // Text twice so a change is always raised; call this after the host dialog has
    // opened so the repaint hits a loaded control.
    public static void SetValue(NumberBox numberBox, double value)
    {
        numberBox.Value = value;
        numberBox.Text = string.Empty;
        numberBox.Text = value.ToString(CultureInfo.CurrentCulture);
    }

    // NumberBox only commits typed text on Enter or focus loss, and a ContentDialog's
    // default button can fire before either happens. Commit the draft text directly.
    public static void CommitTypedText(NumberBox numberBox)
    {
        string? text = FindInputBox(numberBox)?.Text ?? numberBox.Text;
        if (!double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out double parsed))
            return;

        SetValue(numberBox, Math.Clamp(parsed, numberBox.Minimum, numberBox.Maximum));
    }

    private static TextBox? FindInputBox(DependencyObject root)
    {
        if (root is TextBox textBox && textBox.Name == "InputBox")
            return textBox;

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            TextBox? found = FindInputBox(VisualTreeHelper.GetChild(root, i));
            if (found != null)
                return found;
        }

        return null;
    }
}
