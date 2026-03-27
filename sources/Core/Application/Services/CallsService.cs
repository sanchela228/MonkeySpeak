using Core.Domain.Calls;
using Core.Public.Services;

namespace Core.Application.Services;

public class CallsService : ICallsService
{
    public CallSession? Current { get; }
    public event EventHandler<CallSession?>? CurrentChanged;
    public event EventHandler<CallState>? StateChanged;
    public Task<CallSession> CreateAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<CallSession> JoinAsync(string code, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task HangupAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public void SetMicrophoneEnabled(bool enabled)
    {
        throw new NotImplementedException();
    }

    public void SetPlaybackEnabled(bool enabled)
    {
        throw new NotImplementedException();
    }

    public void SetMicrophoneVolume(int percent)
    {
        throw new NotImplementedException();
    }

    public void SetPlaybackVolume(int percent)
    {
        throw new NotImplementedException();
    }
}