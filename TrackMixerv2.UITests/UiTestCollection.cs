using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UiTestCollection : ICollectionFixture<SharedUiAppFixture>
{
    public const string Name = "TrackMixer UI";
}
