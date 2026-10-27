using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace TrackMixerv2;

public static class RecycleBin
{
    private const uint FOFX_RECYCLEONDELETE = 0x00080000;

    /// <summary>
    /// Moves a file to the recycle bin. On volumes without one (network shares, some
    /// removable drives) the shell asks before deleting permanently instead of doing it
    /// silently. Returns true when the file is gone afterwards.
    /// </summary>
    public static unsafe bool TrySend(string path)
    {
        var operation = (IFileOperation)new FileOperation();
        try
        {
            operation.SetOperationFlags(
                FILEOPERATION_FLAGS.FOF_ALLOWUNDO
                | FILEOPERATION_FLAGS.FOF_NOCONFIRMATION
                | FILEOPERATION_FLAGS.FOF_WANTNUKEWARNING
                | (FILEOPERATION_FLAGS)FOFX_RECYCLEONDELETE);

            Guid itemId = typeof(IShellItem).GUID;
            object itemObject;
            fixed (char* pathChars = path)
                PInvoke.SHCreateItemFromParsingName(pathChars, null, &itemId, out itemObject).ThrowOnFailure();
            var item = (IShellItem)itemObject;
            try
            {
                operation.DeleteItem(item, null);
                operation.PerformOperations();
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            Marshal.ReleaseComObject(operation);
        }

        return !File.Exists(path);
    }
}
