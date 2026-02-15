using System;
using System.IO;
using System.Linq;
using Windows.Storage;

namespace TrackMixerv2
{
    public class Helper
    {
        public static readonly string[] VideoExtensions = new string[]
        {
            ".mp4",
            ".m4v",
            ".mkv",
            ".avi",
            ".wmv",
            ".mov",
            ".webm",
            ".mpg",
            ".mpeg",
            ".ts",
            ".m2ts"
        };

        public static string GetTitleFromPath(string path)
        {
            return path.Split("\\").Reverse().ToList()[0];
        }

        public static bool IsVideoFile(StorageFile file)
        {
            if (file == null) return false;
            return VideoExtensions.Contains(file.FileType.ToLowerInvariant());
        }

        public static bool IsSupportedVideoPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return VideoExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
        public static void AppendToDebugFile(string text)
        {
            string testfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackMixerv2", "debug.txt");
            File.AppendAllText(testfile, text);
            File.AppendAllText(testfile, "\n");
        }
    }
}
