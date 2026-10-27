namespace TrackMixerv2.UITests.Infrastructure;

public sealed class PlaybackDiagnostics
{
    public string? Path { get; set; }
    public string State { get; set; } = "";
    public double Position { get; set; }
    public double Duration { get; set; }
    public double Rate { get; set; }
    public TrackDiagnostics? Video { get; set; }
    public List<TrackDiagnostics> Tracks { get; set; } = new();

    public bool IsRunning => State == "Running";

    /// <summary>Largest gap between any two players' positions (video included), in seconds.</summary>
    public double Spread
    {
        get
        {
            var positions = Tracks.Append(Video)
                .Where(track => track?.Position != null)
                .Select(track => track!.Position!.Value)
                .ToList();
            return positions.Count == 0 ? 0 : positions.Max() - positions.Min();
        }
    }

    public override string ToString() =>
        $"{State} {Position:F2}/{Duration:F2}s x{Rate} [{string.Join(", ", Tracks)}]";
}

public sealed class TrackDiagnostics
{
    public int Index { get; set; }
    public string Kind { get; set; } = "";
    public double? Position { get; set; }
    public string? State { get; set; }
    public double? Volume { get; set; }

    public override string ToString() => $"{Index}:{Kind}:{State}@{Position:F2}v{Volume:F2}";
}
