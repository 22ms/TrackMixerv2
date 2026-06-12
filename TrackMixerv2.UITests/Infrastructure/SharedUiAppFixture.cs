namespace TrackMixerv2.UITests.Infrastructure;

public sealed class SharedUiAppFixture : IDisposable
{
    public TrackMixerAppSession Session { get; }
    public string ClipPath { get; }

    public SharedUiAppFixture()
    {
        ClipPath = TrackMixerPaths.CreateTempClipLibrary();
        Session = new TrackMixerAppSession(ClipPath);
        Session.Launch();
    }

    public void Dispose()
    {
        Session.Dispose();
    }
}
