using System.Net;
using System.Net.Sockets;
using System.Text;
using Core.Public.Configurations;

namespace Core.Application.Calls.Infrastructure.Adapters;

public sealed class MainServerStunClient(IConnectionSettingsStore settingsStore) : IStunClient
{
    public async Task<IPEndPoint?> GetPublicEndPointAsync(int localPort, int timeoutMs = 5000, CancellationToken ct = default)
    {
        ConnectionProfile? profile;
        try
        {
            profile = await settingsStore.GetActiveAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Core.Logger.Error("STUN: failed to resolve active connection profile", ex);
            return null;
        }

        if (profile is null)
        {
            Core.Logger.Warn("STUN: no active connection profile");
            return null;
        }

        var domain = NormalizeDomain(profile.Domain);

        try
        {
            using var udp = new UdpClient(localPort);
            udp.Connect(domain, 3478);

            var data = Encoding.UTF8.GetBytes("ping");
            await udp.SendAsync(data, data.Length).ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var result = await udp.ReceiveAsync(linkedCts.Token).ConfigureAwait(false);
            var responseText = Encoding.UTF8.GetString(result.Buffer);

            var ep = IPEndPoint.Parse(responseText);
            Core.Logger.Info($"STUN: localPort={localPort} public={ep}");
            return ep;
        }
        catch (OperationCanceledException)
        {
            Core.Logger.Warn($"STUN: timed out/cancelled (localPort={localPort}, domain={domain})");
            return null;
        }
        catch (SocketException ex)
        {
            Core.Logger.Warn($"STUN: socket error {ex.SocketErrorCode} (localPort={localPort}, domain={domain})");
            return null;
        }
        catch (Exception ex)
        {
            Core.Logger.Error($"STUN: error (localPort={localPort}, domain={domain})", ex);
            return null;
        }
    }

    private static string NormalizeDomain(string domain)
    {
        if (OperatingSystem.IsAndroid() &&
            (string.Equals(domain, "localhost", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(domain, "127.0.0.1", StringComparison.OrdinalIgnoreCase)))
        {
            return "10.0.2.2";
        }

        return domain;
    }
}
