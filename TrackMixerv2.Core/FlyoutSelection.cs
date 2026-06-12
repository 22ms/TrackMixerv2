namespace TrackMixerv2;

public static class FlyoutSelection
{
  public static bool TagMatches(object? tag, string expected) =>
      string.Equals(tag as string, expected, StringComparison.Ordinal);

  public static bool PlaybackRateMatches(object? tag, double rate) =>
      tag is double itemRate && Math.Abs(itemRate - rate) < 0.001;
}
