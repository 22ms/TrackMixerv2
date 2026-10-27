using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace TrackMixerv2;

public static class ContentDialogPresenter
{
    // WinUI throws when a second ContentDialog opens while one is showing (e.g. the root
    // folder prompt from a file activation while Preferences is open), so dialogs queue.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        await Gate.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            Gate.Release();
        }
    }
}
