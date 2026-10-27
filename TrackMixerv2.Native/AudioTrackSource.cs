using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Core;
using Windows.Media.MediaProperties;

namespace TrackMixerv2;

/// <summary>
/// A MediaStreamSource that plays one audio stream of a file without decoding its video.
/// The stream index is the track's AudioTrack.Id. Dispose releases the file handle.
/// </summary>
public sealed class AudioTrackSource : IDisposable
{
    private readonly AudioTrackReader _reader;
    private readonly PeakLimiter _limiter;
    private volatile float _gain = 1f;

    public MediaStreamSource MediaStreamSource { get; }

    /// <summary>
    /// Amplification on top of MediaPlayer.Volume, which stops at 1. Above 1 the
    /// <see cref="PeakLimiter"/> keeps the louder signal from clipping.
    /// </summary>
    public double Gain
    {
        get => _gain;
        set => _gain = (float)Math.Max(1.0, value);
    }

    public AudioTrackSource(string path, uint streamIndex)
    {
        _reader = new AudioTrackReader(path, streamIndex);
        _limiter = new PeakLimiter((int)_reader.SampleRate, (int)_reader.Channels);
        var properties = AudioEncodingProperties.CreatePcm(_reader.SampleRate, _reader.Channels, 32);
        properties.Subtype = MediaEncodingSubtypes.Float;

        MediaStreamSource = new MediaStreamSource(new AudioStreamDescriptor(properties))
        {
            CanSeek = true,
            BufferTime = TimeSpan.Zero,
        };
        if (_reader.Duration > TimeSpan.Zero)
            MediaStreamSource.Duration = _reader.Duration;

        MediaStreamSource.Starting += (_, args) =>
        {
            if (args.Request.StartPosition is TimeSpan start)
            {
                _reader.Seek(start);
                _limiter.Reset();
                args.Request.SetActualStartPosition(start);
            }
        };
        MediaStreamSource.SampleRequested += (_, args) =>
        {
            if (!_reader.TryRead(out byte[] pcm, out TimeSpan timestamp, out TimeSpan duration))
                return;

            _limiter.Process(MemoryMarshal.Cast<byte, float>(pcm.AsSpan()), _gain);

            var sample = MediaStreamSample.CreateFromBuffer(pcm.AsBuffer(), timestamp);
            sample.Duration = duration;
            args.Request.Sample = sample;
        };
        MediaStreamSource.Closed += (_, _) => _reader.Dispose();
    }

    public void Dispose() => _reader.Dispose();
}
