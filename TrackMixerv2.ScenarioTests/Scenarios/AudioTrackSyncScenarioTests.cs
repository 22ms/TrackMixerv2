using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class AudioTrackSyncScenarioTests
{
    [Fact]
    public void Default_sync_constants_match_playback_expectations()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(100), AudioTrackSync.DefaultResyncThreshold);
        Assert.Equal(TimeSpan.FromSeconds(2), AudioTrackSync.SyncCheckInterval);
    }

    [Fact]
    public void ComputeDrift_returns_absolute_difference()
    {
        var master = TimeSpan.FromSeconds(10);
        var ahead = TimeSpan.FromSeconds(10.25);
        var behind = TimeSpan.FromSeconds(9.75);

        Assert.Equal(TimeSpan.FromMilliseconds(250), AudioTrackSync.ComputeDrift(master, ahead));
        Assert.Equal(TimeSpan.FromMilliseconds(250), AudioTrackSync.ComputeDrift(master, behind));
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    [InlineData(500, true)]
    public void ShouldResync_detects_forward_drift_above_threshold(int driftMillis, bool expected)
    {
        var master = TimeSpan.FromSeconds(30);
        var follower = master + TimeSpan.FromMilliseconds(driftMillis);
        var threshold = TimeSpan.FromMilliseconds(100);

        Assert.Equal(expected, AudioTrackSync.ShouldResync(master, follower, threshold));
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void ShouldResync_detects_backward_drift_above_threshold(int driftMillis, bool expected)
    {
        var master = TimeSpan.FromSeconds(30);
        var follower = master - TimeSpan.FromMilliseconds(driftMillis);
        var threshold = TimeSpan.FromMilliseconds(100);

        Assert.Equal(expected, AudioTrackSync.ShouldResync(master, follower, threshold));
    }

    [Fact]
    public void ShouldResyncAny_returns_true_when_any_follower_drifts()
    {
        var master = TimeSpan.FromSeconds(20);
        var inSyncFollowers = new[]
        {
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(20.05),
            TimeSpan.FromSeconds(19.92),
        };
        var driftingFollowers = new[]
        {
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(20.05),
            TimeSpan.FromSeconds(20.2),
        };

        Assert.False(AudioTrackSync.ShouldResyncAny(master, inSyncFollowers, TimeSpan.FromMilliseconds(100)));
        Assert.True(AudioTrackSync.ShouldResyncAny(master, driftingFollowers, TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void ShouldResyncAny_returns_false_for_empty_followers()
    {
        var master = TimeSpan.FromSeconds(15);

        Assert.False(AudioTrackSync.ShouldResyncAny(master, Array.Empty<TimeSpan>(), AudioTrackSync.DefaultResyncThreshold));
    }

    [Fact]
    public void AddOffset_advances_position_within_duration()
    {
        var position = TimeSpan.FromSeconds(10);
        var duration = TimeSpan.FromSeconds(60);

        Assert.Equal(TimeSpan.FromSeconds(12.5), AudioTrackSync.AddOffset(position, 2500, duration));
    }

    [Fact]
    public void AddOffset_rewinds_and_clamps_to_zero()
    {
        var position = TimeSpan.FromSeconds(1);

        Assert.Equal(TimeSpan.Zero, AudioTrackSync.AddOffset(position, -5000, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void AddOffset_does_not_seek_past_natural_duration()
    {
        var position = TimeSpan.FromSeconds(58);
        var duration = TimeSpan.FromSeconds(60);

        Assert.Equal(position, AudioTrackSync.AddOffset(position, 5000, duration));
    }

    [Fact]
    public void AddOffset_allows_seek_when_duration_is_unknown()
    {
        var position = TimeSpan.FromSeconds(58);

        Assert.Equal(TimeSpan.FromSeconds(63), AudioTrackSync.AddOffset(position, 5000, TimeSpan.Zero));
    }

    [Fact]
    public void AddOffset_keeps_position_when_seek_lands_on_duration_boundary()
    {
        var position = TimeSpan.FromSeconds(55);
        var duration = TimeSpan.FromSeconds(60);

        Assert.Equal(position, AudioTrackSync.AddOffset(position, 5000, duration));
    }
}
