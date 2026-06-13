using TrackMixerv2;
using TrackMixerv2.ScenarioTests.Support;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class DeleteVideoScenarioTests : IDisposable
{
    private readonly ClipLibrary _library = new(nameof(DeleteVideoScenarioTests));

    public DeleteVideoScenarioTests()
    {
        AppState.Reset();
        _library.InstallAsRootFolder();
    }

    [Fact]
    public async Task ResolveDeleteTarget_prefers_next_for_middle_clip()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string first = _library.AddClip("first.mp4", baseTime);
        string middle = _library.AddClip("middle.mp4", baseTime.AddMinutes(1));
        string latest = _library.AddClip("latest.mp4", baseTime.AddMinutes(2));

        var config = ChronoConfig();

        Assert.Equal(latest, await ResolveDeleteTargetAsync(config, middle, PlaylistHelper.Direction.Next));
        Assert.Equal(first, await ResolveDeleteTargetAsync(config, middle, PlaylistHelper.Direction.Previous));
    }

    [Fact]
    public async Task ResolveDeleteTarget_falls_back_to_previous_when_deleting_latest_clip()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string first = _library.AddClip("first.mp4", baseTime);
        string latest = _library.AddClip("latest.mp4", baseTime.AddMinutes(5));

        var config = ChronoConfig();

        Assert.Equal(first, await ResolveDeleteTargetAsync(config, latest, PlaylistHelper.Direction.Next));
    }

    [Fact]
    public async Task ResolveDeleteTarget_falls_back_to_next_when_deleting_oldest_clip_in_backward_mode()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string first = _library.AddClip("first.mp4", baseTime);
        string second = _library.AddClip("second.mp4", baseTime.AddMinutes(1));

        var config = ChronoConfig();

        Assert.Equal(second, await ResolveDeleteTargetAsync(config, first, PlaylistHelper.Direction.Previous));
    }

    [Fact]
    public async Task ResolveDeleteTarget_returns_null_when_only_clip_is_deleted()
    {
        string only = _library.AddClip("only.mp4", DateTime.Now.AddDays(-1));
        var config = ChronoConfig();

        Assert.Null(await ResolveDeleteTargetAsync(config, only, PlaylistHelper.Direction.Next));
        Assert.Null(await ResolveDeleteTargetAsync(config, only, PlaylistHelper.Direction.Previous));
    }

    [Fact]
    public void NotifyMediaDeleted_excludes_deleted_file_from_rebuilt_chrono_index()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string first = _library.AddClip("first.mp4", baseTime);
        string latest = _library.AddClip("latest.mp4", baseTime.AddMinutes(5));

        PlaylistIndexCache.GetChrono(_library.RootPath);
        File.Delete(latest);
        PlaylistIndexCache.NotifyMediaDeleted(latest, subfolderOnly: false);

        PlaylistIndex rebuilt = PlaylistIndexCache.GetChrono(_library.RootPath);

        Assert.Single(rebuilt.OrderedPaths);
        Assert.Equal(first, rebuilt.OrderedPaths[0]);
        Assert.False(rebuilt.PathToIndex.ContainsKey(latest));
    }

    [Fact]
    public async Task Delete_flow_simulation_latest_clip_opens_previous_after_file_removed()
    {
        var baseTime = DateTime.Now.AddDays(-2);
        string first = _library.AddClip("first.mp4", baseTime);
        string latest = _library.AddClip("latest.mp4", baseTime.AddMinutes(5));
        var config = ChronoConfig();

        string? target = await ResolveDeleteTargetAsync(config, latest, PlaylistHelper.Direction.Next);
        Assert.Equal(first, target);

        File.Delete(latest);
        PlaylistIndexCache.NotifyMediaDeleted(latest, subfolderOnly: false);
        TrackMetadataStore.RemoveEntry(AppState.TRACK_METADATA, latest);

        Assert.False(File.Exists(latest));
        Assert.True(File.Exists(target));
        Assert.Single(PlaylistIndexCache.GetChrono(_library.RootPath).OrderedPaths);
    }

    private static PlaylistHelper.PlaylistConfig ChronoConfig() =>
        new(PlaylistHelper.PlaylistMode.Chrono, TimeSpan.FromDays(30), subfolderOnly: false);

    private static async Task<string?> ResolveDeleteTargetAsync(
        PlaylistHelper.PlaylistConfig config,
        string deletedPath,
        PlaylistHelper.Direction primary)
    {
        string? target = await PlaylistHelper.GetTrack(config, deletedPath, primary);
        if (target != null)
            return target;

        PlaylistHelper.Direction fallback = primary == PlaylistHelper.Direction.Next
            ? PlaylistHelper.Direction.Previous
            : PlaylistHelper.Direction.Next;

        return await PlaylistHelper.GetTrack(config, deletedPath, fallback);
    }

    public void Dispose()
    {
        _library.Dispose();
        AppState.Reset();
    }
}
