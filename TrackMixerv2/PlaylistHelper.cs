using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace TrackMixerv2
{
    public class PlaylistHelper
    {
        public class PlaylistConfig : INotifyPropertyChanged
        {
            private PlaylistMode _playlistMode;
            public PlaylistMode PlaylistMode
            {
                get { return _playlistMode; }
                set
                {
                    if (_playlistMode != value)
                    {
                        _playlistMode = value;
                        OnPropertyChanged("PlaylistMode");
                    }
                }
            }
            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            private TimeSpan _timeSpan;
            public TimeSpan TimeSpan
            {
                get { return _timeSpan; }
                set
                {
                    if (_timeSpan != value)
                    {
                        _timeSpan = value;
                        OnPropertyChanged("TimeSpan");
                    }
                }
            }

            public bool SubfolderOnly;

            public PlaylistConfig()
            {
                TimeSpan = TimeSpan.FromDays(30);
                PlaylistMode = PlaylistMode.Chrono;
                SubfolderOnly = false;
            }

            public PlaylistConfig(PlaylistMode playlistMode, TimeSpan timeSpan, bool subfolderOnly)
            {
                TimeSpan = timeSpan;
                PlaylistMode = playlistMode;
                SubfolderOnly = subfolderOnly;
            }
        }

        public enum TimeUnit
        {
            Day,
            Week,
            Month,
            Year
        }
        public enum PlaylistMode
        {
            Chrono,
            Rating
        }

        public enum AutoplayMode
        {
            Forward,
            Backward,
            Off
        }

        public enum Direction
        {
            Next,
            Previous
        }

        public static string IsInRatings(PlaylistConfig playlistConfig, string currentFile) // NULL MEANS YES!
        {
            if (currentFile == null || playlistConfig == null) return null;
            string rootFolder = playlistConfig.SubfolderOnly ? Path.GetDirectoryName(currentFile) : MainWindow.RootFoldersContainFile(currentFile);
            DateTime afterThis = DateTime.Now.Subtract(playlistConfig.TimeSpan);
            List<string> sortedVideoFiles = MainWindow.TRACK_METADATA
                .Where(pair => pair.Key.StartsWith(rootFolder) && File.GetCreationTime(pair.Key) > afterThis)
                .OrderByDescending(pair => pair.Value.Rating)
                .Select(pair => pair.Key)
                .ToList();
            if (sortedVideoFiles.Count <= 0) return null;
            bool ratingAboveZero = (MainWindow.TRACK_METADATA.ContainsKey(currentFile) && MainWindow.TRACK_METADATA[currentFile].Rating > 0);
            int currentIndex = sortedVideoFiles.IndexOf(currentFile);
            if (currentIndex != -1 && ratingAboveZero) return null;
            return sortedVideoFiles[0];
        }

        public static string GetTrack(PlaylistConfig playlistConfig, string currentFile, Direction direction)
        {
            if (currentFile == null || playlistConfig == null) return null;

            string rootFolder = playlistConfig.SubfolderOnly ? Path.GetDirectoryName(currentFile) : MainWindow.RootFoldersContainFile(currentFile);
            if (rootFolder == null) return null;

            IEnumerable<string> videoFiles;
            List<string> sortedVideoFiles;
            int currentIndex;

            switch (playlistConfig.PlaylistMode)
            {
                case PlaylistMode.Chrono:
                    videoFiles = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                        .Where(s => s.EndsWith(".mp4") || s.EndsWith(".avi") || s.EndsWith(".mkv"));
                    sortedVideoFiles = videoFiles.OrderBy(f => File.GetCreationTime(f)).ToList();
                    currentIndex = sortedVideoFiles.IndexOf(currentFile);
                    break;
                case PlaylistMode.Rating:
                    DateTime afterThis = DateTime.Now.Subtract(playlistConfig.TimeSpan);
                    sortedVideoFiles = MainWindow.TRACK_METADATA
                        .Where(pair => pair.Key.StartsWith(rootFolder) && File.GetCreationTime(pair.Key) > afterThis)
                        .OrderByDescending(pair => pair.Value.Rating)
                        .Select(pair => pair.Key)
                        .ToList();
                    currentIndex = sortedVideoFiles.IndexOf(currentFile);
                    break;
                default:
                    return null;
            }

            if (direction == Direction.Next)
            {
                if (currentIndex == -1 || currentIndex == sortedVideoFiles.Count - 1)
                {
                    return null;
                }

                return sortedVideoFiles[currentIndex + 1];
            }
            else // Direction.Previous
            {
                if (currentIndex == -1 || currentIndex == 0)
                {
                    return null;
                }

                return sortedVideoFiles[currentIndex - 1];
            }
        }

        public static TimeSpan TimeSpanFromUnitValue(TimeUnit unit, double value)
        {
            TimeSpan span = TimeSpan.FromDays(31);
            switch (unit)
            {
                case TimeUnit.Day:
                    span = TimeSpan.FromDays(value);
                    break;
                case TimeUnit.Week:
                    span = TimeSpan.FromDays(value * 7);
                    break;
                case TimeUnit.Month:
                    span = TimeSpan.FromDays(value * 30);
                    break;
                case TimeUnit.Year:
                    span = TimeSpan.FromDays(value * 365);
                    break;
            }
            return span;
        }

    }
}
