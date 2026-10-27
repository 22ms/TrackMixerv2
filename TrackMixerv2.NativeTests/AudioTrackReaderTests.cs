using System.Runtime.InteropServices;

namespace TrackMixerv2.NativeTests;

public sealed class AudioTrackReaderTests
{
    public static TheoryData<string, int> Tracks()
    {
        var data = new TheoryData<string, int>();
        foreach (string container in new[] { "mkv", "mp4" })
            for (int track = 0; track < Fixtures.TrackFrequencies.Length; track++)
                data.Add(container, track);
        return data;
    }

    [Theory]
    [MemberData(nameof(Tracks))]
    public async Task Each_track_decodes_its_own_audio(string container, int track)
    {
        uint[] streamIds = await Fixtures.AudioStreamIdsAsync(container);
        using var reader = new AudioTrackReader(Fixtures.Path(container), streamIds[track]);

        Assert.Equal(48000u, reader.SampleRate);
        Assert.Equal(2u, reader.Channels);
        Assert.InRange(reader.Duration.TotalSeconds, 19.5, 20.5);

        List<float> samples = ReadSeconds(reader, from: 1, seconds: 1, out _);
        Assert.InRange(
            Fixtures.EstimateFrequency(samples, (int)reader.Channels, reader.SampleRate),
            Fixtures.TrackFrequencies[track] * 0.97,
            Fixtures.TrackFrequencies[track] * 1.03);
    }

    [Theory]
    [InlineData("mkv")]
    [InlineData("mp4")]
    public async Task Seek_lands_exactly_on_the_requested_time_and_keeps_the_track(string container)
    {
        uint[] streamIds = await Fixtures.AudioStreamIdsAsync(container);
        using var reader = new AudioTrackReader(Fixtures.Path(container), streamIds[2]);

        foreach (double target in new[] { 12.0, 3.37, 17.5, 0.0 })
        {
            reader.Seek(TimeSpan.FromSeconds(target));
            Assert.True(reader.TryRead(out byte[] pcm, out TimeSpan first, out TimeSpan duration));
            Assert.InRange(first.TotalSeconds, target - 0.001, target + 0.001);
            Assert.Equal(pcm.Length / 8, (int)Math.Round(duration.TotalSeconds * reader.SampleRate));
        }

        List<float> samples = ReadSeconds(reader, from: 12, seconds: 0.5, out _);
        Assert.InRange(
            Fixtures.EstimateFrequency(samples, 2, reader.SampleRate),
            Fixtures.TrackFrequencies[2] * 0.97,
            Fixtures.TrackFrequencies[2] * 1.03);
    }

    [Fact]
    public async Task Reading_runs_to_the_end_of_the_stream()
    {
        uint[] streamIds = await Fixtures.AudioStreamIdsAsync("mkv");
        using var reader = new AudioTrackReader(Fixtures.Path("mkv"), streamIds[0]);
        reader.Seek(TimeSpan.FromSeconds(19));

        TimeSpan last = TimeSpan.Zero;
        int chunks = 0;
        while (reader.TryRead(out _, out TimeSpan timestamp, out _))
        {
            last = timestamp;
            chunks++;
        }

        Assert.True(chunks > 0);
        Assert.InRange(last.TotalSeconds, 19.5, 20.1);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(9u)]
    public void Non_audio_or_missing_stream_throws(uint streamIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioTrackReader(Fixtures.Path("mkv"), streamIndex));
    }

    [Fact]
    public void Reads_after_dispose_end_the_stream()
    {
        var reader = new AudioTrackReader(Fixtures.Path("mkv"), 1);
        reader.Dispose();
        Assert.False(reader.TryRead(out _, out _, out _));
        reader.Seek(TimeSpan.FromSeconds(1));
    }

    private static List<float> ReadSeconds(AudioTrackReader reader, double from, double seconds, out TimeSpan firstTimestamp)
    {
        reader.Seek(TimeSpan.FromSeconds(from));
        var samples = new List<float>();
        firstTimestamp = TimeSpan.MinValue;
        int wanted = (int)(seconds * reader.SampleRate * reader.Channels);
        while (samples.Count < wanted && reader.TryRead(out byte[] pcm, out TimeSpan timestamp, out _))
        {
            if (firstTimestamp == TimeSpan.MinValue)
                firstTimestamp = timestamp;
            samples.AddRange(MemoryMarshal.Cast<byte, float>(pcm).ToArray());
        }

        return samples;
    }
}
