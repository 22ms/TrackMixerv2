namespace TrackMixerv2.NativeTests;

public sealed class PeakLimiterTests
{
    private const int SampleRate = 48000;

    private static float[] Sine(double amplitude, double seconds) =>
        Enumerable.Range(0, (int)(SampleRate * seconds))
            .SelectMany(i =>
            {
                float value = (float)(amplitude * Math.Sin(2 * Math.PI * 440 * i / SampleRate));
                return new[] { value, value };
            })
            .ToArray();

    [Fact]
    public void Unboosted_audio_passes_through_untouched()
    {
        float[] samples = Sine(0.9, 0.1);
        float[] original = samples.ToArray();

        new PeakLimiter(SampleRate, 2).Process(samples, 1f);

        Assert.Equal(original, samples);
    }

    [Fact]
    public void Quiet_audio_is_amplified_by_the_full_gain()
    {
        float[] samples = Sine(0.2, 0.1);

        new PeakLimiter(SampleRate, 2).Process(samples, 2f);

        Assert.InRange(samples.Max(), 0.39f, 0.41f);
    }

    [Fact]
    public void Loud_boosted_audio_never_exceeds_the_ceiling()
    {
        float[] samples = Sine(0.9, 0.5);

        new PeakLimiter(SampleRate, 2).Process(samples, 2f);

        Assert.All(samples, sample => Assert.InRange(Math.Abs(sample), 0f, PeakLimiter.Ceiling + 1e-6f));
        Assert.True(samples.Max() > 0.9f, "the limiter should hold peaks near the ceiling, not crush them");
    }

    [Fact]
    public void Gain_recovers_after_a_loud_part()
    {
        var limiter = new PeakLimiter(SampleRate, 2);
        limiter.Process(Sine(0.9, 0.2), 2f);

        float[] quiet = Sine(0.2, 0.5);
        limiter.Process(quiet, 2f);

        Assert.InRange(quiet.Skip(quiet.Length / 2).Max(), 0.39f, 0.41f);
    }
}
