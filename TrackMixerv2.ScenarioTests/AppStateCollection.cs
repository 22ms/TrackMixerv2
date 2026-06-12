namespace TrackMixerv2.ScenarioTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppStateCollection : ICollectionFixture<AppStateFixture>
{
    public const string Name = "TrackMixer app state";
}
