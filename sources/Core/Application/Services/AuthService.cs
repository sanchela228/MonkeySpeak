using System.Data;
using System.Text;
using System.Text.Json;
using Core.Application.Abstractions;
using Core.Application.Crypto;
using Core.Public.Configurations;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.AuthCall;

namespace Core.Application.Services;

public class AuthService : IAuthService
{
    private readonly IConnectionService _connection;
    private readonly IKeyStore _keyStore;
    private TaskCompletionSource<bool>? _loginTcs;
    private TaskCompletionSource<bool>? _authTcs;
    private bool _authInProgress;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuthService(IConnectionService connection, IKeyStore keyStore)
    {
        _connection = connection;
        _keyStore = keyStore;
        _connection.MessageReceived += OnMessageReceived;
        _connection.StateChanged += OnConnectionStateChanged;
    }

    public bool IsAuthenticated { get; private set; }
    public string? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? UserCode { get; private set; }

    public event EventHandler? Authenticated;
    public event EventHandler<string>? AuthFailed;
    public event EventHandler? LoggedOut;
    public event EventHandler<string>? UsernameChanged;

    private TaskCompletionSource<string>? _changeUsernameTcs;

    // ── Password flow ────────────────────────────────────────────────────────

    public async Task LoginAsync(string username, string password, CancellationToken ct = default)
    {
        _loginTcs = new TaskCompletionSource<bool>();

        var msg = new LoginRequest { Username = username, Password = password, Value = "Login request" };
        await SendAsync(msg, ct);

        Core.Logger.Info($"LoginRequest sent for user: {username}");

        using var reg = ct.Register(() => _loginTcs.TrySetCanceled());
        await _loginTcs.Task;
    }

    // ── KeyPair flow ─────────────────────────────────────────────────────────

    public async Task RegisterIfNeededAsync(CancellationToken ct = default)
    {
        if (_authInProgress) return;
        _authInProgress = true;
        try
        {
            var existingUserId = await _keyStore.GetUserIdAsync();
            if (!string.IsNullOrEmpty(existingUserId))
            {
                await AuthenticateWithKeyPairAsync(existingUserId, ct);
                return;
            }

            var (privEd, pubEd, _, pubX) = await _keyStore.GetOrCreateKeysAsync().ContinueWith(t =>
            {
                var r = t.Result;
                return (r.privateEd25519, r.publicEd25519, (byte[]?)null, r.publicX25519);
            }, ct);

            var username = await _keyStore.GetOrCreateUsernameAsync();
            var nonce = Guid.NewGuid().ToString();
            var signature = UserCrypto.Sign(privEd, Encoding.UTF8.GetBytes(nonce));

            _authTcs = new TaskCompletionSource<bool>();

            var msg = new RegisterKey
            {
                Username = username,
                PublicKeyEd25519Base64 = Convert.ToBase64String(pubEd),
                PublicKeyX25519Base64 = Convert.ToBase64String(pubX),
                ProofSignature = Convert.ToBase64String(signature),
                Nonce = nonce,
                Value = "Register key"
            };
            await SendAsync(msg, ct);

            Core.Logger.Info($"RegisterKey sent for username: {username}");

            using var reg = ct.Register(() => _authTcs.TrySetCanceled());
            await _authTcs.Task;
        }
        finally
        {
            _authInProgress = false;
        }
    }

    public async Task AuthenticateAsync(CancellationToken ct = default)
    {
        var settings = _connection.ServerSettings;
        if (settings is null)
        {
            Core.Logger.Warn("AuthenticateAsync: no ServerSettings yet");
            return;
        }

        if (settings.AuthMode != AuthMode.KeyPair) return;

        if (_authInProgress) return;
        _authInProgress = true;
        try
        {
            var userId = await _keyStore.GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                _authInProgress = false;
                await RegisterIfNeededAsync(ct);
                return;
            }

            await AuthenticateWithKeyPairAsync(userId, ct);
        }
        finally
        {
            _authInProgress = false;
        }
    }

    private async Task AuthenticateWithKeyPairAsync(string userId, CancellationToken ct)
    {
        _authTcs = new TaskCompletionSource<bool>();

        var msg = new RequestAuth { UserId = userId, Value = "Request authentication" };
        await SendAsync(msg, ct);

        Core.Logger.Info($"RequestAuth sent for userId: {userId}");

        using var reg = ct.Register(() => _authTcs.TrySetCanceled());
        await _authTcs.Task;
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    public Task LogoutAsync(CancellationToken ct = default)
    {
        ResetAuthState();
        return Task.CompletedTask;
    }

    public async Task ChangeUsernameAsync(string newUsername, CancellationToken ct = default)
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException("Not authenticated");

        _changeUsernameTcs = new TaskCompletionSource<string>();

        var msg = new ChangeUsername { NewUsername = newUsername, Value = "Change username" };
        await SendAsync(msg, ct);

        using var reg = ct.Register(() => _changeUsernameTcs.TrySetCanceled());
        var result = await _changeUsernameTcs.Task;
        Username = result;
    }

    // ── Connection state ─────────────────────────────────────────────────────

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        if (state is ConnectionState.Closed or ConnectionState.Broken)
        {
            if (IsAuthenticated || _authInProgress)
            {
                _authInProgress = false;
                _loginTcs?.TrySetCanceled();
                _authTcs?.TrySetCanceled();
                ResetAuthState();
            }
        }
    }

    private void ResetAuthState()
    {
        if (!IsAuthenticated) return;
        IsAuthenticated = false;
        UserId = null;
        Username = null;
        UserCode = null;
        Core.Logger.Info("Auth state reset");
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    // ── Message handlers ─────────────────────────────────────────────────────

    private void OnMessageReceived(object? sender, string raw)
    {
        try
        {
            var ctx = JsonSerializer.Deserialize<Context>(raw, JsonOpts);
            if (ctx is null) return;

            switch (ctx.Type)
            {
                case "Messages.AuthCall.LoginSuccess":   HandleLoginSuccess(ctx); break;
                case "Messages.AuthCall.Authenticated":  HandleAuthenticated(ctx); break;
                case "Messages.AuthCall.KeyRegistered":  _ = HandleKeyRegisteredAsync(ctx); break;
                case "Messages.AuthCall.AuthChallenge":  _ = HandleAuthChallengeAsync(ctx); break;
                case "Messages.AuthCall.UsernameChanged":   HandleUsernameChanged(ctx); break;
                case "Messages.AuthCall.ErrorRegistration": HandleError(ctx); break;
            }
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"AuthService message parse error: {ex.Message}");
        }
    }

    private void HandleLoginSuccess(Context ctx)
    {
        var msg = ctx.Message.Deserialize<LoginSuccess>(JsonOpts);
        if (msg is null) return;

        IsAuthenticated = true;
        UserId = msg.UserId;
        Username = msg.Username;
        UserCode = msg.UserCode;

        Core.Logger.Info($"Login success: {msg.Username} ({msg.UserId})");
        _loginTcs?.TrySetResult(true);
        Authenticated?.Invoke(this, EventArgs.Empty);
    }

    private void HandleAuthenticated(Context ctx)
    {
        var msg = ctx.Message.Deserialize<Authenticated>(JsonOpts);
        if (msg is null) return;

        IsAuthenticated = true;
        UserId = msg.UserId;
        Username = msg.Username;
        UserCode = msg.UserCode;

        Core.Logger.Info($"KeyPair auth success: {msg.Username} ({msg.UserId})");
        _authTcs?.TrySetResult(true);
        Authenticated?.Invoke(this, EventArgs.Empty);
    }

    private async Task HandleKeyRegisteredAsync(Context ctx)
    {
        var msg = ctx.Message.Deserialize<KeyRegistered>(JsonOpts);
        if (msg is null) return;

        var username = await _keyStore.GetOrCreateUsernameAsync();
        await _keyStore.SaveRegistrationAsync(msg.UserId, username, msg.Fingerprint);

        IsAuthenticated = true;
        UserId = msg.UserId;
        Username = username;
        UserCode = msg.UserCode;

        Core.Logger.Info($"Key registered: userId={msg.UserId} fingerprint={msg.Fingerprint}");
        _authTcs?.TrySetResult(true);
        Authenticated?.Invoke(this, EventArgs.Empty);
    }

    private async Task HandleAuthChallengeAsync(Context ctx)
    {
        var msg = ctx.Message.Deserialize<AuthChallenge>(JsonOpts);
        if (msg is null) return;

        try
        {
            var (privEd, _, _) = await _keyStore.GetOrCreateKeysAsync();
            var signature = UserCrypto.Sign(privEd, Encoding.UTF8.GetBytes(msg.Nonce));
            var userId = await _keyStore.GetUserIdAsync();

            var response = new Authenticate
            {
                UserId = userId ?? string.Empty,
                Signature = Convert.ToBase64String(signature),
                Value = "Authentication response"
            };
            await SendAsync(response);

            Core.Logger.Info("AuthChallenge response sent");
        }
        catch (Exception ex)
        {
            Core.Logger.Error("AuthChallenge signing failed", ex);
            _authTcs?.TrySetException(ex);
            AuthFailed?.Invoke(this, $"SIGNING_FAILED: {ex.Message}");
        }
    }

    private void HandleUsernameChanged(Context ctx)
    {
        var msg = ctx.Message.Deserialize<UsernameChanged>(JsonOpts);
        if (msg is null) return;

        Username = msg.NewUsername;
        Core.Logger.Info($"Username changed to: {msg.NewUsername}");
        _changeUsernameTcs?.TrySetResult(msg.NewUsername);
        UsernameChanged?.Invoke(this, msg.NewUsername);
    }

    private void HandleError(Context ctx)
    {
        var msg = ctx.Message.Deserialize<ErrorRegistration>(JsonOpts);
        if (msg is null) return;

        var error = $"{msg.ErrorCode}: {msg.Value}";
        Core.Logger.Warn($"Auth error: {error}");

        _loginTcs?.TrySetException(new InvalidOperationException(error));
        _authTcs?.TrySetException(new InvalidOperationException(error));
        _changeUsernameTcs?.TrySetException(new InvalidOperationException(error));
        AuthFailed?.Invoke(this, error);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SendAsync(IMessage message, CancellationToken ct = default)
    {
        var ctx = Context.Create(message);
        var json = JsonSerializer.Serialize(ctx);
        await _connection.SendAsync(json, ct);
    }
}
