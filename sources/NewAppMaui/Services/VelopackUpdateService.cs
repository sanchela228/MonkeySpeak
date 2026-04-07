#if WINDOWS
using Velopack;
using Velopack.Sources;

namespace NewAppMaui.Services;

public class VelopackUpdateService
{
    private const string RepoUrl = "https://github.com/sanchela228/MonkeySpeak.app";

    private readonly UpdateManager _mgr = new(new GithubSource(RepoUrl, null, false));

    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            return await _mgr.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Update check failed: {ex.Message}");
            return null;
        }
    }

    public async Task DownloadAsync(UpdateInfo info, Action<int>? progress = null)
    {
        await _mgr.DownloadUpdatesAsync(info, p => progress?.Invoke(p));
    }

    public void ApplyAndRestart(UpdateInfo info)
    {
        _mgr.ApplyUpdatesAndRestart(info);
    }
}
#endif
