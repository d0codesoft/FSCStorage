using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace fsc.mob.client.Auth;

public sealed class ConnectionSessionService
{
    private const string ServerUrlKey = "fsc.mob.client.server-url";
    private const string ApiTokenKey = "fsc.mob.client.api-token";

    private readonly SemaphoreSlim _sync = new(1, 1);

    private bool _isInitialized;
    private string _serverUrl = string.Empty;
    private string? _apiToken;
    private string? _sessionCookieHeader;
    private AuthMode _authMode = AuthMode.ApiToken;

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(_apiToken) ||
        !string.IsNullOrWhiteSpace(_sessionCookieHeader);

    public string ServerUrl => _serverUrl;
    public AuthMode AuthMode => _authMode;

    public async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        await _sync.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            _serverUrl = NormalizeServerUrl(await SecureStorage.Default.GetAsync(ServerUrlKey))
                ?? GetDefaultServerUrl();
            _apiToken = await SecureStorage.Default.GetAsync(ApiTokenKey);
            _authMode = string.IsNullOrWhiteSpace(_apiToken)
                ? AuthMode.AdminCredentials
                : AuthMode.ApiToken;
            _isInitialized = true;
        }
        finally
        {
            _sync.Release();
        }
    }

    public ConnectionProfile GetProfile()
    {
        return new ConnectionProfile
        {
            ServerUrl = string.IsNullOrWhiteSpace(_serverUrl)
                ? GetDefaultServerUrl()
                : _serverUrl,
            ApiToken = _apiToken,
            AuthMode = _authMode
        };
    }

    public async Task SaveApiTokenSessionAsync(string serverUrl, string apiToken, bool persistToken)
    {
        await EnsureInitializedAsync();

        _serverUrl = NormalizeServerUrl(serverUrl) ?? GetDefaultServerUrl();
        _apiToken = apiToken.Trim();
        _sessionCookieHeader = null;
        _authMode = AuthMode.ApiToken;

        await SecureStorage.Default.SetAsync(ServerUrlKey, _serverUrl);

        if (persistToken)
            await SecureStorage.Default.SetAsync(ApiTokenKey, _apiToken);
        else
            SecureStorage.Default.Remove(ApiTokenKey);
    }

    public async Task SaveCredentialSessionAsync(string serverUrl, string sessionCookieHeader)
    {
        await EnsureInitializedAsync();

        _serverUrl = NormalizeServerUrl(serverUrl) ?? GetDefaultServerUrl();
        _sessionCookieHeader = sessionCookieHeader;
        _apiToken = null;
        _authMode = AuthMode.AdminCredentials;

        await SecureStorage.Default.SetAsync(ServerUrlKey, _serverUrl);
        SecureStorage.Default.Remove(ApiTokenKey);
    }

    public async Task SaveServerUrlOnlyAsync(string serverUrl)
    {
        await EnsureInitializedAsync();
        _serverUrl = NormalizeServerUrl(serverUrl) ?? GetDefaultServerUrl();
        await SecureStorage.Default.SetAsync(ServerUrlKey, _serverUrl);
    }

    public async Task DisconnectAsync(bool clearSavedToken)
    {
        await EnsureInitializedAsync();

        _sessionCookieHeader = null;

        if (clearSavedToken)
        {
            _apiToken = null;
            SecureStorage.Default.Remove(ApiTokenKey);
        }
    }

    public bool TryGetApiToken(out string? apiToken)
    {
        apiToken = _apiToken;
        return !string.IsNullOrWhiteSpace(apiToken);
    }

    public bool TryGetSessionCookieHeader(out string? sessionCookieHeader)
    {
        sessionCookieHeader = _sessionCookieHeader;
        return !string.IsNullOrWhiteSpace(sessionCookieHeader);
    }

    public string GetRequiredServerUrl()
    {
        return NormalizeServerUrl(_serverUrl)
            ?? throw new InvalidOperationException("Server URL is not configured.");
    }

    public static string? NormalizeServerUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim().TrimEnd('/');
        return Uri.TryCreate($"{trimmed}/", UriKind.Absolute, out var uri)
            ? uri.ToString().TrimEnd('/')
            : null;
    }

    public static string GetDefaultServerUrl()
    {
        return DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5770"
            : "http://127.0.0.1:5770";
    }
}
