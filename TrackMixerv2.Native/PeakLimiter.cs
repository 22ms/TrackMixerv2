namespace TrackMixerv2;

/// <summary>
/// Applies a gain to interleaved float samples and keeps the result under <see cref="Ceiling"/>.
/// Peaks are pulled down at once and the gain recovers over <see cref="Release"/>, so boosted
/// audio gets quieter on loud parts instead of crackling. The export runs ffmpeg's alimiter with
/// the same ceiling and release.
/// </summary>
public sealed class PeakLimiter(int sampleRate, int channels)
{
    public const float Ceiling = 0.98f;
    public static readonly TimeSpan Release = TimeSpan.FromMilliseconds(50);

    private readonly float _releaseStep = 1f - MathF.Exp(-1f / (float)(Release.TotalSeconds * sampleRate));
    private float _reduction = 1f;

    public void Reset() => _reduction = 1f;

    public void Process(Span<float> samples, float gain)
    {
        if (gain <= 1f && _reduction >= 1f)
            return;

        for (int frame = 0; frame + channels <= samples.Length; frame += channels)
        {
            float peak = 0f;
            for (int c = 0; c < channels; c++)
                peak = MathF.Max(peak, MathF.Abs(samples[frame + c]));

            peak *= gain;
            float target = peak > Ceiling ? Ceiling / peak : 1f;
            _reduction = target < _reduction ? target : _reduction + (target - _reduction) * _releaseStep;
            if (_reduction > 0.9999f)
                _reduction = 1f;

            float scale = gain * _reduction;
            for (int c = 0; c < channels; c++)
                samples[frame + c] *= scale;
        }
    }
}
