using System.ComponentModel;

namespace TrackMixerv2;

public class PlaylistHelper
{
    public static Func<Task<bool>>? EnsureRootFolderAsync { get; set; }

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
        public event PropertyChangedEventHandler? PropertyChanged;
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

    public static string? IsInRatings(PlaylistConfig playlistConfig, string? currentFile)
    {
        if (currentFile == null || playlistConfig == null) return null;
        string? rootFolder = playlistConfig.SubfolderOnly ? Path.GetDirectoryName(currentFile) : AppState.RootFoldersContainFile(currentFile);
        if (rootFolder == null) return null;

        DateTime afterThis = DateTime.Now.Subtract(playlistConfig.TimeSpan);
        List<string> sortedVideoFiles = AppState.TRACK_METADATA
            .Where(pair => Helper.PathIsUnderDirectory(pair.Key, rootFolder) && File.GetCreationTime(pair.Key) > afterThis)
            .OrderByDescending(pair => pair.Value.Rating)
            .Select(pair => pair.Key)
            .ToList();
        if (sortedVideoFiles.Count <= 0) return null;
        bool ratingAboveZero = (AppState.TRACK_METADATA.ContainsKey(currentFile) && AppState.TRACK_METADATA[currentFile].Rating > 0);
        int currentIndex = sortedVideoFiles.IndexOf(currentFile);
        if (currentIndex != -1 && ratingAboveZero) return null;
        return sortedVideoFiles[0];
    }

    public static async Task<string?> GetTrack(PlaylistConfig playlistConfig, string? currentFile, Direction direction)
    {
        if (AppState.ROOT_FOLDERS == null || AppState.ROOT_FOLDERS.Count == 0)
        {
            if (AppState.RootFolderPromptSuppressed)
                return null;

            if (EnsureRootFolderAsync == null)
                return null;

            bool success = await EnsureRootFolderAsync();
            if (!success) return null;
        }

        if (currentFile == null || playlistConfig == null) return null;

        string? rootFolder = playlistConfig.SubfolderOnly ? Path.GetDirectoryName(currentFile) : AppState.RootFoldersContainFile(currentFile);
        if (rootFolder == null) return null;

        IEnumerable<string> videoFiles;
        List<string> sortedVideoFiles;
        int currentIndex;

        switch (playlistConfig.PlaylistMode)
        {
            case PlaylistMode.Chrono:
                videoFiles = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                    .Where(Helper.IsSupportedVideoPath);
                sortedVideoFiles = videoFiles.OrderBy(f => File.GetCreationTime(f)).ToList();
                currentIndex = sortedVideoFiles.IndexOf(currentFile);
                break;
            case PlaylistMode.Rating:
                DateTime afterThis = DateTime.Now.Subtract(playlistConfig.TimeSpan);
                sortedVideoFiles = AppState.TRACK_METADATA
                    .Where(pair => Helper.PathIsUnderDirectory(pair.Key, rootFolder) && File.GetCreationTime(pair.Key) > afterThis)
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
                return null;

            return sortedVideoFiles[currentIndex + 1];
        }

        if (currentIndex == -1 || currentIndex == 0)
            return null;

        return sortedVideoFiles[currentIndex - 1];
    }

    public static TimeSpan TimeSpanFromUnitValue(TimeUnit unit, double value)
    {
        return unit switch
        {
            TimeUnit.Day => TimeSpan.FromDays(value),
            TimeUnit.Week => TimeSpan.FromDays(value * 7),
            TimeUnit.Month => TimeSpan.FromDays(value * 30),
            TimeUnit.Year => TimeSpan.FromDays(value * 365),
            _ => TimeSpan.FromDays(31),
        };
    }
}
