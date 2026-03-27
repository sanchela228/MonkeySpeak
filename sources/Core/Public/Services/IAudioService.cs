using Core.Domain.Media;

namespace Core.Public.Services;

public interface IAudioService : IDisposable
{
    void Initialize();
    bool IsInitialized { get; }

    void StartCapture();
    void StopCapture();
    bool IsCapturing { get; }

    /// <summary>Opus packet ready to send over network. CallsService subscribes → UdpUnifiedManager.</summary>
    event Action<byte[]>? OnEncodedAudioReady;

    /// <summary>Decode+play opus from remote peer. Creates per-interlocutor channel on first call.</summary>
    void HandleReceivedAudio(string interlocutorId, byte[] opusData);
    void RemoveInterlocutor(string interlocutorId);

    IReadOnlyList<AudioDeviceInfo> CaptureDevices { get; }
    IReadOnlyList<AudioDeviceInfo> PlaybackDevices { get; }
    void RefreshDevices();
    void SwitchCaptureDevice(string deviceId);
    void SwitchPlaybackDevice(string deviceId);

    bool IsMicrophoneEnabled { get; set; }
    bool IsPlaybackEnabled { get; set; }
    int MicrophoneVolume { get; set; }   // 0–200 %
    int PlaybackVolume { get; set; }     // 0–200 %

    float SelfAudioLevel { get; }
    float GetInterlocutorAudioLevel(string interlocutorId);
}
