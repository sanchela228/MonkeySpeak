using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Concentus.Enums;
using Concentus.Structs;
using Core.Application.Media;
using Core.Domain.Media;
using Core.Public.Services;
using RNNoise.NET;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace Core.Application.Services;

public sealed class AudioService : IAudioService
{
    private static readonly AudioFormat Format = new()
    {
        SampleRate = 48000,
        Channels = 1,
        Format = SampleFormat.F32
    };

    private const int FrameDurationMs = 10;
    private const int FrameSizePerChannel = 48000 / (1000 / FrameDurationMs); // 480
    private const int MaxOpusPacketBytes = 4096;
    private const int OpusBitrate = 64000;

    private MiniAudioEngine? _engine;
    private AudioCaptureDevice? _captureDevice;
    private AudioPlaybackDevice? _playbackDevice;

    private OpusEncoder? _encoder;
    private Denoiser? _denoiser;

    private readonly List<float> _rnnoiseBuffer = new(FrameSizePerChannel);
    private readonly List<float> _opusBuffer = new(FrameSizePerChannel);
    private readonly byte[] _opusPacket = new byte[MaxOpusPacketBytes];

    private readonly ConcurrentDictionary<string, InterlocutorAudioChannel> _channels = new();

    private readonly ConcurrentQueue<Action> _mixerActions = new();
    private Thread? _audioThread;
    private volatile bool _running;
    private volatile bool _engineReady;
    private readonly object _readyLock = new();

    private string? _selectedCaptureDeviceId;
    private string? _selectedPlaybackDeviceId;

    private volatile bool _micEnabled = true;
    private volatile bool _playbackEnabled = true;
    private volatile int _micVolume = 100;
    private volatile int _playbackVolume = 100;

    private float _selfAudioLevel;
    private long _selfLevelTicks;
    private long _encodedPacketCount;

    public bool IsInitialized => _engineReady;
    public bool IsCapturing => _captureDevice != null && _micEnabled;

    public event Action<byte[]>? OnEncodedAudioReady;

    public event Action<bool>? MicrophoneEnabledChanged;

    public bool IsMicrophoneEnabled
    {
        get => _micEnabled;
        set
        {
            if (_micEnabled == value)
                return;

            _micEnabled = value;
            if (!value)
            {
                lock (_rnnoiseBuffer) _rnnoiseBuffer.Clear();
                lock (_opusBuffer) _opusBuffer.Clear();
            }
            Logger.Info($"[Audio] Mic {(value ? "enabled" : "disabled")}");

            try { MicrophoneEnabledChanged?.Invoke(value); } catch { }
        }
    }

    public bool IsPlaybackEnabled
    {
        get => _playbackEnabled;
        set
        {
            _playbackEnabled = value;
            if (value)
            {
                foreach (var ch in _channels.Values)
                    try { ch.Stream.Clear(); } catch { }
            }
            Logger.Info($"[Audio] Playback {(value ? "enabled" : "disabled")}");
        }
    }

    public int MicrophoneVolume
    {
        get => _micVolume;
        set => _micVolume = Math.Clamp(value, 0, 200);
    }

    public int PlaybackVolume
    {
        get => _playbackVolume;
        set => _playbackVolume = Math.Clamp(value, 0, 200);
    }

    public float SelfAudioLevel
    {
        get
        {
            var elapsed = DateTime.UtcNow - new DateTime(Interlocked.Read(ref _selfLevelTicks), DateTimeKind.Utc);
            return elapsed.TotalMilliseconds > 500 ? 0f : _selfAudioLevel;
        }
    }

    public IReadOnlyList<AudioDeviceInfo> CaptureDevices { get; private set; } = [];
    public IReadOnlyList<AudioDeviceInfo> PlaybackDevices { get; private set; } = [];

    public void Initialize()
    {
        if (_engineReady) return;

        _audioThread = new Thread(AudioThreadLoop) { IsBackground = true, Name = "AudioService" };
        _running = true;
        _audioThread.Start();

        lock (_readyLock)
        {
            if (!_engineReady)
                Monitor.Wait(_readyLock, TimeSpan.FromSeconds(5));
        }

        Logger.Info("[Audio] Initialized");
    }

    public void StartCapture()
    {
        _micEnabled = true;
        Logger.Info("[Audio] Capture started");
    }

    public void StopCapture()
    {
        _micEnabled = false;
        lock (_rnnoiseBuffer) _rnnoiseBuffer.Clear();
        lock (_opusBuffer) _opusBuffer.Clear();
        Logger.Info("[Audio] Capture stopped");
    }

    public void RefreshDevices()
    {
        if (_engine == null) return;

        _mixerActions.Enqueue(() =>
        {
            _engine.UpdateDevicesInfo();
            CaptureDevices = MapDevices(_engine.CaptureDevices);
            PlaybackDevices = MapDevices(_engine.PlaybackDevices);
        });
    }

    public void SwitchCaptureDevice(string deviceId)
    {
        _selectedCaptureDeviceId = deviceId;

        _mixerActions.Enqueue(() =>
        {
            if (_engine == null) return;

            _engine.UpdateDevicesInfo();
            var selected = FindDevice(_engine.CaptureDevices, deviceId);

            try
            {
                if (_captureDevice != null)
                {
                    _captureDevice.OnAudioProcessed -= OnCaptureProcessed;
                    _captureDevice.Stop();
                    _captureDevice.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Audio] Error stopping old capture device: {ex.Message}");
            }

            _captureDevice = _engine.InitializeCaptureDevice(selected, Format);
            _captureDevice.OnAudioProcessed += OnCaptureProcessed;
            _captureDevice.Start();

            lock (_rnnoiseBuffer) _rnnoiseBuffer.Clear();
            lock (_opusBuffer) _opusBuffer.Clear();

            Logger.Info($"[Audio] Switched capture to {selected?.Name ?? "Default"}");
        });
    }

    public void SwitchPlaybackDevice(string deviceId)
    {
        _selectedPlaybackDeviceId = deviceId;

        _mixerActions.Enqueue(() =>
        {
            if (_engine == null) return;

            _engine.UpdateDevicesInfo();
            var selected = FindDevice(_engine.PlaybackDevices, deviceId);

            foreach (var (id, channel) in _channels)
            {
                try
                {
                    lock (channel)
                    {
                        if (channel.DedicatedPlaybackDevice != null)
                        {
                            try { channel.DedicatedPlaybackDevice.MasterMixer.RemoveComponent(channel.Player); } catch { }
                            try { channel.DedicatedPlaybackDevice.Stop(); } catch { }
                            try { channel.DedicatedPlaybackDevice.Dispose(); } catch { }
                        }

                        var newDevice = _engine.InitializePlaybackDevice(selected, Format);
                        if (newDevice == null)
                        {
                            Logger.Warn($"[Audio] Failed to init playback for channel {id}");
                            continue;
                        }

                        channel.DedicatedPlaybackDevice = newDevice;
                        newDevice.Start();
                        newDevice.MasterMixer.AddComponent(channel.Player);
                        channel.Player.Play();
                        try { channel.Stream.Clear(); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[Audio] Error rebinding channel {id}: {ex.Message}");
                }
            }

            Logger.Info($"[Audio] Switched playback to {selected?.Name ?? "Default"}");
        });
    }

    private long _receivedPacketCount;
    private long _lastRecvLogTicks;

    public void HandleReceivedAudio(string interlocutorId, byte[] opusData)
    {
        var count = Interlocked.Increment(ref _receivedPacketCount);

        if (!_channels.TryGetValue(interlocutorId, out var channel))
        {
            Logger.Info($"[Audio] First packet from {interlocutorId}, opusLen={opusData.Length}, creating channel...");

            var created = CreateChannelOnAudioThread(interlocutorId);
            if (created == null)
            {
                Logger.Error($"[Audio] Channel creation FAILED for {interlocutorId}");
                return;
            }

            channel = _channels.GetOrAdd(interlocutorId, created);
            if (!ReferenceEquals(channel, created))
            {
                try
                {
                    _mixerActions.Enqueue(() =>
                    {
                        try { lock (created) created.Dispose(); } catch { }
                    });
                }
                catch { }
            }
        }

        lock (channel)
        {
            var decoded = new float[FrameSizePerChannel];
            int samples = channel.DecodeAndMeasure(decoded, opusData, FrameSizePerChannel);

            // Log every 200 packets
            if (count % 200 == 1)
            {
                float peak = 0f;
                for (int i = 0; i < samples; i++)
                    peak = Math.Max(peak, Math.Abs(decoded[i]));
                Logger.Debug($"[Audio] Recv #{count}: samples={samples} peak={peak:F4} playbackEnabled={_playbackEnabled}");
            }

            float gain = _playbackVolume / 100f;
            if (Math.Abs(gain - 1.0f) > 0.001f && samples > 0)
            {
                for (int i = 0; i < samples; i++)
                {
                    if (float.IsNaN(decoded[i]) || float.IsInfinity(decoded[i]))
                        decoded[i] = 0f;
                    else
                        decoded[i] = Math.Clamp(decoded[i] * gain, -1.0f, 1.0f);
                }
            }

            if (_playbackEnabled && samples > 0)
            {
                ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes<float>(decoded.AsSpan(0, samples));
                var buf = new byte[bytes.Length];
                bytes.CopyTo(buf);
                channel.Stream.Write(buf, 0, buf.Length);
            }
        }
    }

    public void RemoveInterlocutor(string interlocutorId)
    {
        if (!_channels.TryRemove(interlocutorId, out var channel))
            return;

        try
        {
            _mixerActions.Enqueue(() =>
            {
                try { lock (channel) channel.Dispose(); } catch { }
                Logger.Info($"[Audio] Removed channel for {interlocutorId}");
            });
        }
        catch
        {
        }
    }

    public float GetInterlocutorAudioLevel(string interlocutorId)
    {
        if (!_channels.TryGetValue(interlocutorId, out var channel))
            return 0f;

        var elapsed = DateTime.UtcNow - channel.LastAudioReceived;
        return elapsed.TotalMilliseconds < 500 ? channel.AudioLevel : 0f;
    }

    private void AudioThreadLoop()
    {
        try
        {
            _engine = new MiniAudioEngine();

            _captureDevice = _engine.InitializeCaptureDevice(null, Format);
            _playbackDevice = _engine.InitializePlaybackDevice(null, Format);

            _denoiser = new Denoiser();

            _encoder = new OpusEncoder(Format.SampleRate, Format.Channels, OpusApplication.OPUS_APPLICATION_VOIP)
            {
                Bitrate = OpusBitrate,
                Complexity = 8,
                SignalType = OpusSignal.OPUS_SIGNAL_VOICE
            };

            _captureDevice.OnAudioProcessed += OnCaptureProcessed;
            _playbackDevice.Start();
            _captureDevice.Start();

            _engine.UpdateDevicesInfo();
            CaptureDevices = MapDevices(_engine.CaptureDevices);
            PlaybackDevices = MapDevices(_engine.PlaybackDevices);

            lock (_readyLock)
            {
                _engineReady = true;
                Monitor.PulseAll(_readyLock);
            }

            Logger.Info("[Audio] Engine ready");

            while (_running)
            {
                bool processed = false;
                while (_mixerActions.TryDequeue(out var action))
                {
                    try { action(); }
                    catch (Exception ex) { Logger.Error($"[Audio] Mixer action error: {ex.Message}"); }
                    processed = true;
                }

                if (!processed) Thread.Sleep(10);
            }

            StopDevices();
        }
        catch (Exception ex)
        {
            Logger.Error($"[Audio] Engine thread fatal: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// SoundFlow capture callback. Runs on audio thread.
    /// Pipeline: gain → normalize → RMS → (RNNoise) → Opus encode → OnEncodedAudioReady event.
    /// </summary>
    private void OnCaptureProcessed(Span<float> samples, Capability capability)
    {
        if (!_micEnabled) return;

        float[] input = samples.ToArray();

        float gain = _micVolume / 100f;
        if (Math.Abs(gain - 1.0f) > 0.001f)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (float.IsNaN(input[i]) || float.IsInfinity(input[i]))
                    input[i] = 0f;
                else
                    input[i] = Math.Clamp(input[i] * gain, -1.0f, 1.0f);
            }
        }

        for (int i = 0; i < input.Length; i++)
        {
            if (float.IsNaN(input[i]) || float.IsInfinity(input[i]))
                input[i] = 0f;
            else
                input[i] = Math.Clamp(input[i], -1.0f, 1.0f);
        }

        float rms = 0f;
        for (int i = 0; i < input.Length; i++)
            rms += input[i] * input[i];

        if (input.Length > 0)
        {
            rms = (float)Math.Sqrt(rms / input.Length);
            _selfAudioLevel = Math.Clamp(rms * 3f, 0f, 1f);
            Interlocked.Exchange(ref _selfLevelTicks, DateTime.UtcNow.Ticks);
        }

        lock (_rnnoiseBuffer)
        {
            _rnnoiseBuffer.AddRange(input);

            while (_rnnoiseBuffer.Count >= FrameSizePerChannel)
            {
                float[] frame = _rnnoiseBuffer.GetRange(0, FrameSizePerChannel).ToArray();
                _rnnoiseBuffer.RemoveRange(0, FrameSizePerChannel);

                Span<float> span = frame.AsSpan();
                _denoiser!.Denoise(span, finish: false);

                lock (_opusBuffer)
                {
                    _opusBuffer.AddRange(frame);
                }
            }
        }

        lock (_opusBuffer)
        {
            while (_opusBuffer.Count >= FrameSizePerChannel)
            {
                float[] frame = _opusBuffer.GetRange(0, FrameSizePerChannel).ToArray();
                _opusBuffer.RemoveRange(0, FrameSizePerChannel);

                int encoded = _encoder!.Encode(frame, 0, FrameSizePerChannel, _opusPacket, 0, MaxOpusPacketBytes);
                if (encoded > 0)
                {
                    var pktNum = Interlocked.Increment(ref _encodedPacketCount);
                    if (pktNum == 1 || pktNum % 200 == 0)
                        Logger.Debug($"[Audio] Encoded #{pktNum}: {encoded} bytes, rms={_selfAudioLevel:F4}");

                    var packet = new byte[encoded];
                    Buffer.BlockCopy(_opusPacket, 0, packet, 0, encoded);
                    OnEncodedAudioReady?.Invoke(packet);
                }
            }
        }
    }

    private InterlocutorAudioChannel? CreateChannel(string interlocutorId)
    {
        lock (_readyLock)
        {
            if (!_engineReady && !Monitor.Wait(_readyLock, TimeSpan.FromSeconds(5)))
            {
                Logger.Error($"[Audio] Timeout waiting for engine, cannot create channel for {interlocutorId}");
                return null;
            }
        }

        if (_engine == null) return null;

        try
        {
            var stream = new ProducerConsumerStream();
            var dataProvider = new RawDataProvider(stream, Format.Format, Format.SampleRate, Format.Channels);

            DeviceInfo? selected = _selectedPlaybackDeviceId != null
                ? FindDevice(_engine.PlaybackDevices, _selectedPlaybackDeviceId)
                : null;

            var device = _engine.InitializePlaybackDevice(selected, Format);
            if (device == null)
            {
                Logger.Error($"[Audio] Failed to init playback device for {interlocutorId}");
                return null;
            }

            device.Start();

            var player = new SoundPlayer(_engine, Format, dataProvider);
            device.MasterMixer.AddComponent(player);
            player.Play();

            var channel = new InterlocutorAudioChannel(
                new OpusDecoder(Format.SampleRate, Format.Channels),
                stream, dataProvider, player, device);

            Logger.Info($"[Audio] Channel created for {interlocutorId}");
            return channel;
        }
        catch (Exception ex)
        {
            Logger.Error($"[Audio] Failed to create channel for {interlocutorId}: {ex.Message}", ex);
            return null;
        }
    }

    private InterlocutorAudioChannel? CreateChannelOnAudioThread(string interlocutorId)
    {
        try
        {
            var tcs = new TaskCompletionSource<InterlocutorAudioChannel?>(TaskCreationOptions.RunContinuationsAsynchronously);

            _mixerActions.Enqueue(() =>
            {
                try
                {
                    var ch = CreateChannel(interlocutorId);
                    tcs.TrySetResult(ch);
                }
                catch
                {
                    tcs.TrySetResult(null);
                }
            });

            if (!tcs.Task.Wait(TimeSpan.FromSeconds(5)))
                return null;

            return tcs.Task.Result;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<AudioDeviceInfo> MapDevices(DeviceInfo[] devices)
    {
        var list = new List<AudioDeviceInfo>(devices.Length);
        foreach (var d in devices)
            list.Add(new AudioDeviceInfo(d.Id.ToString(), d.Name, d.IsDefault));
        return list;
    }

    private static DeviceInfo? FindDevice(DeviceInfo[] devices, string id)
    {
        foreach (var d in devices)
        {
            if (d.Id.ToString() == id)
                return d;
        }
        return null;
    }

    private void StopDevices()
    {
        try { _captureDevice?.Stop(); } catch { }
        try { _playbackDevice?.Stop(); } catch { }

        foreach (var ch in _channels.Values)
            try { ch.Player.Stop(); } catch { }
    }

    public void Dispose()
    {
        _running = false;
        _micEnabled = false;

        StopDevices();

        _audioThread?.Join(2000);

        foreach (var (_, channel) in _channels)
            try { channel.Dispose(); } catch { }

        _channels.Clear();

        try { _captureDevice?.Dispose(); } catch { }
        try { _playbackDevice?.Dispose(); } catch { }
        try { _engine?.Dispose(); } catch { }
        try { _denoiser?.Dispose(); } catch { }

        Logger.Info("[Audio] Disposed");
    }
}
