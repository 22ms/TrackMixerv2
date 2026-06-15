using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

/// <summary>
/// Tests the multi-track synchronisation rules that govern when and how auxiliary
/// MediaPlayer positions are corrected relative to the main player.
///
/// These are pure-logic tests that run without any WinUI runtime dependencies.
/// They document the expected behaviour of the sync algorithm so that regressions
/// in the mathematical logic surface before the UI layer is involved.
/// </summary>
public sealed class MultiTrackSyncRulesScenarioTests
{
    // -------------------------------------------------------------------------
    // ShouldResync – boundary and edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void ShouldResync_returns_false_when_follower_exactly_matches_master()
    {
        var position = TimeSpan.FromSeconds(45);
        Assert.False(AudioTrackSync.ShouldResync(position, position, AudioTrackSync.DefaultResyncThreshold));
    }

    [Fact]
    public void ShouldResync_returns_false_when_drift_equals_threshold_exactly()
    {
        var master = TimeSpan.FromSeconds(10);
        var follower = master + AudioTrackSync.DefaultResyncThreshold;
        Assert.False(AudioTrackSync.ShouldResync(master, follower, AudioTrackSync.DefaultResyncThreshold));
    }

    [Fact]
    public void ShouldResync_returns_true_when_drift_exceeds_threshold_by_one_tick()
    {
        var master = TimeSpan.FromSeconds(10);
        var follower = master + AudioTrackSync.DefaultResyncThreshold + TimeSpan.FromTicks(1);
        Assert.True(AudioTrackSync.ShouldResync(master, follower, AudioTrackSync.DefaultResyncThreshold));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void ShouldResync_is_symmetric_for_forward_and_backward_drift(int driftSeconds)
    {
        var master = TimeSpan.FromSeconds(30);
        var threshold = TimeSpan.FromSeconds(0.5);
        var ahead = master + TimeSpan.FromSeconds(driftSeconds);
        var behind = master - TimeSpan.FromSeconds(Math.Min(driftSeconds, 30));

        Assert.Equal(
            AudioTrackSync.ShouldResync(master, ahead, threshold),
            AudioTrackSync.ShouldResync(master, behind, threshold));
    }

    // -------------------------------------------------------------------------
    // ShouldResyncAny – multi-follower scenarios
    // -------------------------------------------------------------------------

    [Fact]
    public void ShouldResyncAny_returns_false_when_all_followers_are_within_threshold()
    {
        var master = TimeSpan.FromSeconds(20);
        var threshold = AudioTrackSync.DefaultResyncThreshold;
        var followers = new[]
        {
            master,
            master + TimeSpan.FromMilliseconds(50),
            master - TimeSpan.FromMilliseconds(80),
        };

        Assert.False(AudioTrackSync.ShouldResyncAny(master, followers, threshold));
    }

    [Fact]
    public void ShouldResyncAny_returns_true_when_only_the_last_follower_drifts()
    {
        var master = TimeSpan.FromSeconds(20);
        var threshold = AudioTrackSync.DefaultResyncThreshold;
        var followers = new[]
        {
            master,
            master + TimeSpan.FromMilliseconds(50),
            master + TimeSpan.FromMilliseconds(200), // only this one drifts
        };

        Assert.True(AudioTrackSync.ShouldResyncAny(master, followers, threshold));
    }

    [Fact]
    public void ShouldResyncAny_returns_true_when_only_the_first_follower_drifts()
    {
        var master = TimeSpan.FromSeconds(20);
        var threshold = AudioTrackSync.DefaultResyncThreshold;
        var followers = new[]
        {
            master - TimeSpan.FromSeconds(1), // this one drifts
            master,
            master + TimeSpan.FromMilliseconds(50),
        };

        Assert.True(AudioTrackSync.ShouldResyncAny(master, followers, threshold));
    }

    [Fact]
    public void ShouldResyncAny_with_single_follower_matches_ShouldResync()
    {
        var master = TimeSpan.FromSeconds(30);
        var threshold = AudioTrackSync.DefaultResyncThreshold;
        var followerInSync = master + TimeSpan.FromMilliseconds(50);
        var followerDrifted = master + TimeSpan.FromSeconds(1);

        Assert.Equal(
            AudioTrackSync.ShouldResync(master, followerInSync, threshold),
            AudioTrackSync.ShouldResyncAny(master, [followerInSync], threshold));

        Assert.Equal(
            AudioTrackSync.ShouldResync(master, followerDrifted, threshold),
            AudioTrackSync.ShouldResyncAny(master, [followerDrifted], threshold));
    }

    // -------------------------------------------------------------------------
    // ComputeDrift
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeDrift_is_zero_when_positions_match()
    {
        var position = TimeSpan.FromSeconds(15);
        Assert.Equal(TimeSpan.Zero, AudioTrackSync.ComputeDrift(position, position));
    }

    [Fact]
    public void ComputeDrift_is_always_non_negative()
    {
        var master = TimeSpan.FromSeconds(10);
        var ahead = master + TimeSpan.FromSeconds(5);
        var behind = master - TimeSpan.FromSeconds(3);

        Assert.True(AudioTrackSync.ComputeDrift(master, ahead) >= TimeSpan.Zero);
        Assert.True(AudioTrackSync.ComputeDrift(master, behind) >= TimeSpan.Zero);
    }

    // -------------------------------------------------------------------------
    // AddOffset – boundary conditions that relate directly to multi-track seeks
    // -------------------------------------------------------------------------

    [Fact]
    public void AddOffset_returns_zero_when_negative_offset_exceeds_current_position()
    {
        var position = TimeSpan.FromSeconds(2);
        var result = AudioTrackSync.AddOffset(position, -10_000, TimeSpan.FromSeconds(60));
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void AddOffset_does_not_advance_past_natural_duration_leaving_position_unchanged()
    {
        // A seek that would land on or beyond the end is clamped – position stays put.
        var duration = TimeSpan.FromSeconds(60);
        var nearEnd = duration - TimeSpan.FromMilliseconds(500);

        var result = AudioTrackSync.AddOffset(nearEnd, 1_000, duration);
        Assert.Equal(nearEnd, result);
    }

    [Fact]
    public void AddOffset_accepts_zero_offset_without_change()
    {
        var position = TimeSpan.FromSeconds(30);
        var duration = TimeSpan.FromSeconds(60);

        Assert.Equal(position, AudioTrackSync.AddOffset(position, 0, duration));
    }

    [Fact]
    public void AddOffset_clamps_exactly_at_duration_boundary()
    {
        // Landing exactly on the duration boundary also leaves position unchanged.
        var duration = TimeSpan.FromSeconds(60);
        var position = TimeSpan.FromSeconds(55);
        int offsetMs = (int)(duration - position).TotalMilliseconds; // exactly fills to end

        Assert.Equal(position, AudioTrackSync.AddOffset(position, offsetMs, duration));
    }

    // -------------------------------------------------------------------------
    // Constants sanity – ensure the resync interval is longer than the threshold
    // so a single polling tick cannot be beaten by normal drift accumulation.
    // -------------------------------------------------------------------------

    [Fact]
    public void SyncCheckInterval_is_longer_than_DefaultResyncThreshold()
    {
        Assert.True(
            AudioTrackSync.SyncCheckInterval > AudioTrackSync.DefaultResyncThreshold,
            "Sync polling interval should exceed the resync threshold so the timer " +
            "only fires when genuine drift has accumulated, not on every tick.");
    }
}
