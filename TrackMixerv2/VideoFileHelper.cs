using System.Linq;
using Windows.Storage;

namespace TrackMixerv2
{
    public static class VideoFileHelper
    {
        public static bool IsVideoFile(StorageFile file)
        {
            if (file == null) return false;
            return Helper.VideoExtensions.Contains(file.FileType.ToLowerInvariant());
        }
    }
}
