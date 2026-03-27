using Core.Domain.Calls;

namespace Core.Public.Services;

public interface ICallsService
{
    CallSession? Current { get; }
    event EventHandler<CallSession?>? CurrentChanged;
    event EventHandler<CallState>? StateChanged;

    Task<CallSession> CreateAsync(CancellationToken ct = default);
    Task<CallSession> JoinAsync(string code, CancellationToken ct = default);
    Task HangupAsync(CancellationToken ct = default);

    void SetMicrophoneEnabled(bool enabled);
    void SetPlaybackEnabled(bool enabled);

    void SetMicrophoneVolume(int percent);   // 0..100
    void SetPlaybackVolume(int percent);     // 0..100
}