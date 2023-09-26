using System;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Storage;

namespace TrackMixerv2
{
    public class Helper
    {
        public static string GetTitleFromPath(string path)
        {
            return path.Split("\\").Reverse().ToList()[0];
        }
        public static bool IsVideoFile(StorageFile file)
        {
            var videoExtensions = new string[] { ".mp4", ".wmv", ".avi" }; // add more if needed
            return videoExtensions.Contains(file.FileType.ToLower());
        }
    }
}
