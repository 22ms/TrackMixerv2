using System;
using System.IO;
using System.Linq;
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
        public static void AppendToDebugFile(string text)
        {
            string testfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackMixerv2", "debug.txt");
            File.AppendAllText(testfile, text);
            File.AppendAllText(testfile, "\n");
        }
    }
}
