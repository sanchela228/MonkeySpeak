using Concentus.Structs;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Components;
using SoundFlow.Providers;

namespace Core.Application.Media;

internal sealed class InterlocutorAudioChannel : IDisposable
{
    public OpusDecoder Decoder { get; }
    public ProducerConsumerStream Stream { get; }
    public RawDataProvider DataProvider { get; }
    public SoundPlayer Player { get; }
    public AudioPlaybackDevice DedicatedPlaybackDevice { get; set; }

    public float AudioLevel { get; private set; }
    public DateTime LastAudioReceived { get; private set; } = DateTime.UtcNow;

    public InterlocutorAudioChannel(
        OpusDecoder decoder,
        ProducerConsumerStream stream,
        RawDataProvider dataProvider,
        SoundPlayer player,
        AudioPlaybackDevice dedicatedPlaybackDevice)
    {
        Decoder = decoder;
        Stream = stream;
        DataProvider = dataProvider;
        Player = player;
        DedicatedPlaybackDevice = dedicatedPlaybackDevice;
    }

    /// <summary>
    /// Decode Opus packet, compute RMS level, return decoded float samples.
    /// </summary>
    public int DecodeAndMeasure(float[] decodedFrame, byte[] opusData, int frameSizePerChannel)
    {
        int decoded = Decoder.Decode(opusData, 0, opusData.Length, decodedFrame, 0, frameSizePerChannel, false);

        float sum = 0f;
        for (int i = 0; i < decoded; i++)
            sum += decodedFrame[i] * decodedFrame[i];

        float rms = decoded > 0 ? (float)Math.Sqrt(sum / decoded) : 0f;
        AudioLevel = Math.Clamp(rms * 3f, 0f, 1f);
        LastAudioReceived = DateTime.UtcNow;

        return decoded;
    }

    public void Dispose()
    {
        try { Player.Stop(); } catch { }
        try { Player.Dispose(); } catch { }
        try { DedicatedPlaybackDevice.Stop(); } catch { }
        try { DedicatedPlaybackDevice.Dispose(); } catch { }
        try { DataProvider.Dispose(); } catch { }
        try { Stream.Dispose(); } catch { }
        try { Decoder.Dispose(); } catch { }
    }
}
