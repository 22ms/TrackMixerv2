using TrackMixerv2.UITests.Infrastructure;

namespace TrackMixerv2.UITests.Tests;

[Collection(UiTestPersistenceCollection.Name)]
[Trait("Category", "UI")]
public sealed class EmptyStateUiTests : IDisposable
{
    private readonly string _testHome;
    private TrackMixerAppSession? _session;

    public EmptyStateUiTests()
    {
        _testHome = TrackMixerPaths.CreateIsolatedTestHome();
    }

    [Fact]
    public void Launch_without_clip_shows_empty_state_greeting()
    {
        _session = new TrackMixerAppSession(deleteClipDirectoryOnDispose: false);
        _session.SetTestStorageDirectory(_testHome);
        _session.Launch();

        UiWait.Until(
            () => _session.FindByAutomationId("EmptyStateOpenButton"),
            TimeSpan.FromSeconds(15),
            "empty state open button");

        Assert.NotNull(_session.FindByAutomationId("EmptyStateOpenButton"));
        Assert.Equal(0, _session.GetTabCount());
    }

    public void Dispose()
    {
        _session?.Dispose();
        if (Directory.Exists(_testHome))
            Directory.Delete(_testHome, recursive: true);
    }
}
