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
        string? rootFolder = ResolveRootFolder(playlistConfig, currentFile);
        if (rootFolder == null) return null;

        DateTime afterThis = DateTime.Now.Subtract(playlistConfig.TimeSpan);
        PlaylistIndex playlist = PlaylistIndexCache.GetRating(rootFolder, afterThis);
        if (playlist.OrderedPaths.Count <= 0) return null;

        bool ratingAboveZero = AppState.TRACK_METADATA.TryGetValue(currentFile, out TrackMetadata? metadata)
            && metadata.Rating > 0;
        bool hasIndex = playlist.TryGetIndex(currentFile, out int currentIndex);
        if (hasIndex && ratingAboveZero) return null;
        return playlist.OrderedPaths[0];
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

        string? rootFolder = ResolveRootFolder(playlistConfig, currentFile);
        if (rootFolder == null) return null;

        PlaylistIndex playlist;
        switch (playlistConfig.PlaylistMode)
        {
            case PlaylistMode.Chrono:
                playlist = PlaylistIndexCache.GetChronoOrRebuild(rootFolder, currentFile);
                break;
            case PlaylistMode.Rating:
                playlist = PlaylistIndexCache.GetRating(rootFolder, DateTime.Now.Subtract(playlistConfig.TimeSpan));
                break;
            default:
                return null;
        }

        if (!playlist.TryGetIndex(currentFile, out int currentIndex))
            currentIndex = -1;

        if (direction == Direction.Next)
        {
            if (currentIndex == -1 || currentIndex == playlist.OrderedPaths.Count - 1)
                return null;

            return playlist.OrderedPaths[currentIndex + 1];
        }

        if (currentIndex == -1 || currentIndex == 0)
            return null;

        return playlist.OrderedPaths[currentIndex - 1];
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

    private static string? ResolveRootFolder(PlaylistConfig playlistConfig, string currentFile) =>
        playlistConfig.SubfolderOnly
            ? Path.GetDirectoryName(currentFile)
            : AppState.RootFoldersContainFile(currentFile);

    public static void PrewarmPlaylistIndex(PlaylistConfig? playlistConfig, string? currentFile)
    {
        if (playlistConfig == null || string.IsNullOrWhiteSpace(currentFile))
            return;

        Task.Run(() => WarmPlaylistIndex(playlistConfig, currentFile));
    }

    static void WarmPlaylistIndex(PlaylistConfig playlistConfig, string currentFile)
    {
        string? rootFolder = ResolveRootFolder(playlistConfig, currentFile);
        if (rootFolder == null)
            return;

        switch (playlistConfig.PlaylistMode)
        {
            case PlaylistMode.Chrono:
                PlaylistIndexCache.GetChronoOrRebuild(rootFolder, currentFile);
                break;
            case PlaylistMode.Rating:
                PlaylistIndexCache.GetRating(rootFolder, DateTime.Now.Subtract(playlistConfig.TimeSpan));
                break;
        }
    }
}
