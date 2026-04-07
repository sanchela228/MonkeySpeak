using Core.Application.Calls.Networking;
using Core.Domain.Calls;

namespace Core.Public.Services;

public interface ICallsService
{
    CallSession? Current { get; }
    event EventHandler<CallSession?>? CurrentChanged;
    event EventHandler<CallState>? StateChanged;

    event Action<string, byte[]>? AvatarReceived;
    event Action<string, bool>? MuteStateChanged;
    event Action<string, string>? DisplayNameReceived;
    event Action<string, ChatMessage>? ChatMessageReceived;

    void SendChatMessage(string text);

    Task<CallSession> CreateAsync(CancellationToken ct = default);
    Task<CallSession> JoinAsync(string code, CancellationToken ct = default);
    Task HangupAsync(CancellationToken ct = default);
}