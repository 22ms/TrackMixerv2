namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class VolumeScaleScenarioTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(110, 130)]
    [InlineData(120, 160)]
    public void Positions_map_to_volume(double position, double volume)
    {
        Assert.Equal(volume, VolumeScale.ToVolume(position), 6);
        Assert.Equal(position, VolumeScale.ToPosition(volume), 6);
    }

    [Fact]
    public void Full_volume_sits_at_three_quarters_of_the_slider()
    {
        Assert.Equal(0.75, 100 / VolumeScale.MaxPosition, 6);
        Assert.Equal(VolumeScale.MaxVolume, VolumeScale.ToVolume(VolumeScale.MaxPosition), 6);
    }

    [Fact]
    public void Saved_volumes_outside_the_range_are_clamped()
    {
        Assert.Equal(VolumeScale.MaxPosition, VolumeScale.ToPosition(500), 6);
        Assert.Equal(0, VolumeScale.ToPosition(-5), 6);
    }
}
