using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Media.MediaFoundation;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;

namespace TrackMixerv2;

/// <summary>
/// Decodes one audio stream of a media file to 32-bit float PCM with Media Foundation and
/// never touches the video stream, so seeking only demuxes instead of decoding video.
/// The stream index is Media Foundation's, which is what WinRT exposes as AudioTrack.Id.
/// It is not the container order: the MP4 source lists audio streams in reverse.
/// </summary>
public sealed class AudioTrackReader : IDisposable
{
    private const uint AllStreams = 0xFFFFFFFE;
    private const uint MediaSourceStream = 0xFFFFFFFF;
    private const int MF_E_INVALIDSTREAMNUMBER = unchecked((int)0xC00D36B3);
    private static readonly Lazy<bool> MediaFoundationStarted = new(() =>
    {
        PInvoke.MFStartup(PInvoke.MF_VERSION, 0).ThrowOnFailure();
        return true;
    });

    private readonly object _lock = new();
    private readonly uint _streamIndex;
    private IMFSourceReader? _reader;
    private TimeSpan? _discardBefore;

    public uint SampleRate { get; }
    public uint Channels { get; }
    public TimeSpan Duration { get; }

    public unsafe AudioTrackReader(string path, uint streamIndex)
    {
        _ = MediaFoundationStarted.Value;
        PInvoke.MFCreateSourceReaderFromURL(path, null, out IMFSourceReader reader).ThrowOnFailure();
        _reader = reader;
        try
        {
            reader.SetStreamSelection(AllStreams, false);
            EnsureAudioStream(reader, streamIndex);
            _streamIndex = streamIndex;
            reader.SetStreamSelection(_streamIndex, true);

            PInvoke.MFCreateMediaType(out IMFMediaType pcm).ThrowOnFailure();
            pcm.SetGUID(PInvoke.MF_MT_MAJOR_TYPE, PInvoke.MFMediaType_Audio);
            pcm.SetGUID(PInvoke.MF_MT_SUBTYPE, PInvoke.MFAudioFormat_Float);
            reader.SetCurrentMediaType(_streamIndex, null, pcm);
            Marshal.ReleaseComObject(pcm);

            reader.GetCurrentMediaType(_streamIndex, out IMFMediaType actual);
            actual.GetUINT32(PInvoke.MF_MT_AUDIO_SAMPLES_PER_SECOND, out uint sampleRate);
            actual.GetUINT32(PInvoke.MF_MT_AUDIO_NUM_CHANNELS, out uint channels);
            Marshal.ReleaseComObject(actual);
            SampleRate = sampleRate;
            Channels = channels;
            Duration = ReadDuration(reader);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Returns false at the end of the stream or after disposal.</summary>
    public bool TryRead(out byte[] pcm, out TimeSpan timestamp, out TimeSpan duration)
    {
        lock (_lock)
        {
            while (_reader != null)
            {
                _reader.ReadSample(_streamIndex, 0, out _, out uint flags, out long time, out IMFSample? sample);
                if ((flags & (uint)MF_SOURCE_READER_FLAG.MF_SOURCE_READERF_ENDOFSTREAM) != 0)
                {
                    if (sample != null)
                        Marshal.ReleaseComObject(sample);
                    break;
                }

                if (sample == null)
                    continue;

                try
                {
                    sample.GetSampleDuration(out long sampleDuration);
                    timestamp = TimeSpan.FromTicks(time);
                    duration = TimeSpan.FromTicks(sampleDuration);
                    if (_discardBefore is TimeSpan target && timestamp + duration <= target)
                        continue;

                    pcm = CopyBuffer(sample);
                    if (_discardBefore is TimeSpan start && timestamp < start)
                        pcm = TrimLeadingFrames(pcm, start - timestamp, ref timestamp, ref duration);
                    _discardBefore = null;
                    return true;
                }
                finally
                {
                    Marshal.ReleaseComObject(sample);
                }
            }
        }

        pcm = [];
        timestamp = TimeSpan.Zero;
        duration = TimeSpan.Zero;
        return false;
    }

    public unsafe void Seek(TimeSpan position)
    {
        lock (_lock)
        {
            if (_reader == null)
                return;

            var value = new PROPVARIANT();
            value.Anonymous.Anonymous.vt = VARENUM.VT_I8;
            value.Anonymous.Anonymous.Anonymous.hVal = Math.Max(0, position.Ticks);
            Guid timeFormat = Guid.Empty;
            _reader.SetCurrentPosition(&timeFormat, in value);
            // Containers seek to the previous cluster or sync point (MKV can land seconds
            // early), so samples before the target are dropped here.
            _discardBefore = TimeSpan.FromTicks(Math.Max(0, position.Ticks));
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_reader == null)
                return;

            Marshal.ReleaseComObject(_reader);
            _reader = null;
        }
    }

    private static unsafe byte[] CopyBuffer(IMFSample sample)
    {
        sample.ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
        try
        {
            byte* data;
            uint maxLength;
            uint length;
            buffer.Lock(&data, &maxLength, &length);
            try
            {
                return new ReadOnlySpan<byte>(data, (int)length).ToArray();
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            Marshal.ReleaseComObject(buffer);
        }
    }

    private byte[] TrimLeadingFrames(byte[] pcm, TimeSpan skip, ref TimeSpan timestamp, ref TimeSpan duration)
    {
        int frameBytes = (int)Channels * sizeof(float);
        int skipFrames = (int)Math.Round(skip.TotalSeconds * SampleRate);
        int skipBytes = Math.Min(pcm.Length, skipFrames * frameBytes);
        TimeSpan skipped = TimeSpan.FromSeconds(skipBytes / frameBytes / (double)SampleRate);
        timestamp += skipped;
        duration -= skipped;
        return pcm[skipBytes..];
    }

    private static void EnsureAudioStream(IMFSourceReader reader, uint streamIndex)
    {
        IMFMediaType type;
        try
        {
            reader.GetNativeMediaType(streamIndex, 0, out type);
        }
        catch (COMException ex) when (ex.HResult == MF_E_INVALIDSTREAMNUMBER)
        {
            throw new ArgumentOutOfRangeException(nameof(streamIndex), $"Stream {streamIndex} does not exist.");
        }

        type.GetGUID(PInvoke.MF_MT_MAJOR_TYPE, out Guid majorType);
        Marshal.ReleaseComObject(type);
        if (majorType != PInvoke.MFMediaType_Audio)
            throw new ArgumentOutOfRangeException(nameof(streamIndex), $"Stream {streamIndex} is not audio.");
    }

    private static TimeSpan ReadDuration(IMFSourceReader reader)
    {
        try
        {
            reader.GetPresentationAttribute(MediaSourceStream, PInvoke.MF_PD_DURATION, out PROPVARIANT value);
            return TimeSpan.FromTicks((long)value.Anonymous.Anonymous.Anonymous.uhVal);
        }
        catch (COMException)
        {
            return TimeSpan.Zero;
        }
    }
}
