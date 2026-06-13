using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class PlaybackRatesScenarioTests : IDisposable
{

    private readonly string _settingsPath;



    public PlaybackRatesScenarioTests()

    {

        _settingsPath = Path.Combine(

            Path.GetTempPath(),

            "TrackMixerScenarioTests",

            Guid.NewGuid().ToString("N"),

            "local_settings.json");

        Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, _settingsPath);

        LocalSettingsStore.ResetCache();

        LocalSettingsStore.ResetTransportRates();

    }



    [Fact]

    public void Default_transport_rates_match_built_in_list()

    {

        Assert.Equal(PlaybackRates.Defaults, PlaybackRates.All);

    }



    [Fact]

    public void Boost_rates_match_transport_bar_speeds_above_1x()

    {

        Assert.Equal(

            new[] { 1.25, 1.5, 1.75, 2, 2.5, 3, 3.5, 4, 4.5, 5, 6, 7, 8 },

            PlaybackRates.BoostRates.ToArray());

    }



    [Fact]

    public void Slow_rates_match_transport_bar_speeds_below_1x()

    {

        Assert.Equal(new[] { 0.25, 0.5, 0.75 }, PlaybackRates.SlowRates.ToArray());

    }



    [Theory]

    [InlineData(2.3, 2.5)]

    [InlineData(0.1, 1.25)]

    [InlineData(10, 8)]

    public void Normalize_boost_rate_snaps_to_nearest_transport_speed(double input, double expected) =>

        Assert.Equal(expected, PlaybackRates.NormalizeBoostRate(input));



    [Theory]

    [InlineData(0.6, 0.5)]

    [InlineData(1, 0.75)]

    public void Normalize_slow_rate_snaps_to_nearest_transport_speed(double input, double expected) =>

        Assert.Equal(expected, PlaybackRates.NormalizeSlowRate(input));



    [Fact]

    public void Sanitize_transport_rates_adds_1x_sorts_and_deduplicates()

    {

        var sanitized = PlaybackRates.SanitizeTransportRates(new[] { 2.0, 0.5, 2, 3.5 });



        Assert.Equal(new[] { 0.5, 1.0, 2.0, 3.5 }, sanitized);

    }



    [Fact]

    public void Custom_transport_rates_flow_through_settings_and_hold_lists()

    {

        LocalSettingsStore.SetTransportRates(new[] { 0.5, 1, 3, 6 });

        LocalSettingsStore.ResetCache();



        Assert.Equal(new[] { 0.5, 1.0, 3.0, 6.0 }, PlaybackRates.All);

        Assert.Equal(new[] { 3.0, 6.0 }, PlaybackRates.BoostRates.ToArray());

        Assert.Equal(new[] { 0.5 }, PlaybackRates.SlowRates.ToArray());

    }



    public void Dispose()

    {

        Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, null);

        LocalSettingsStore.ResetCache();



        string? directory = Path.GetDirectoryName(_settingsPath);

        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))

            Directory.Delete(directory, recursive: true);

    }

}


