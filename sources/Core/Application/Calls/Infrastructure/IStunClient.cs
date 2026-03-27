using System.Net;

namespace Core.Application.Calls.Infrastructure;

public interface IStunClient
{
    Task<IPEndPoint?> GetPublicEndPointAsync(int localPort, int timeoutMs = 5000, CancellationToken ct = default);
}
