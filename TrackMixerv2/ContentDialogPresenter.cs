using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace TrackMixerv2;

public static class ContentDialogPresenter
{
    public static async Task<ContentDialogResult?> TryShowAsync(ContentDialog dialog)
    {
        if (UiTestBootstrap.IsEnabled)
            return null;

        if (!ContentDialogShowGate.TryAcquire())
            return null;

        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            ContentDialogShowGate.Release();
        }
    }
}
