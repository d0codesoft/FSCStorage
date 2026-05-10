using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using fsc.mob.client.Auth;

namespace fsc.mob.client.Api;

public sealed class FscAdminApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ConnectionSessionService _sessionService;

    public FscAdminApiClient(HttpClient httpClient, ConnectionSessionService sessionService)
    {
        _httpClient = httpClient;
        _sessionService = sessionService;
    }

    public async Task TestApiTokenAsync(string serverUrl, string apiToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateAbsoluteRequest(HttpMethod.Get, serverUrl, "ui-api/storage/statistics?largestFilesLimit=5");
        request.Headers.Add("X-Api-Key", apiToken.Trim());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AuthSignInResult> SignInAsync(string serverUrl, string login, string password, string? twoFactorCode, CancellationToken cancellationToken = default)
    {
        using var request = CreateAbsoluteRequest(HttpMethod.Post, serverUrl, "auth/login");
        request.Content = JsonContent.Create(
            new LoginRequest
            {
                Login = login,
                Password = password,
                Remember = false,
                TwoFactorCode = string.IsNullOrWhiteSpace(twoFactorCode) ? null : twoFactorCode.Trim()
            },
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw CreateApiException(response.StatusCode, content);

        var challenge = TryReadJson<LoginChallengeResponse>(content);
        if (challenge?.RequiresTwoFactor == true)
        {
            return new AuthSignInResult
            {
                RequiresTwoFactor = true,
                TwoFactorMethod = challenge.TwoFactorMethod,
                ChallengeToken = challenge.ChallengeToken,
                ChallengeExpiresUtc = challenge.ChallengeExpiresUtc
            };
        }

        var user = TryReadJson<MeResponse>(content)
            ?? throw new InvalidOperationException("Sign-in response did not include user information.");

        return new AuthSignInResult
        {
            User = user,
            SessionCookieHeader = ReadSessionCookieHeader(response)
        };
    }

    public async Task<AuthSignInResult> VerifyTwoFactorAsync(string serverUrl, string challengeToken, string code, CancellationToken cancellationToken = default)
    {
        using var request = CreateAbsoluteRequest(HttpMethod.Post, serverUrl, "auth/two-factor");
        request.Content = JsonContent.Create(
            new VerifyTwoFactorLoginRequest
            {
                ChallengeToken = challengeToken,
                Code = code,
                Remember = false
            },
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw CreateApiException(response.StatusCode, content);

        var user = TryReadJson<MeResponse>(content)
            ?? throw new InvalidOperationException("Two-factor response did not include user information.");

        return new AuthSignInResult
        {
            User = user,
            SessionCookieHeader = ReadSessionCookieHeader(response)
        };
    }

    public async Task SignOutAsync(string serverUrl, string sessionCookieHeader, CancellationToken cancellationToken = default)
    {
        using var request = CreateAbsoluteRequest(HttpMethod.Post, serverUrl, "auth/logout");
        request.Headers.Add("Cookie", sessionCookieHeader);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return;

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<StorageStatisticsViewModel> GetStorageStatisticsAsync(int largestFilesLimit = 10, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Get, $"ui-api/storage/statistics?largestFilesLimit={largestFilesLimit}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<StorageStatisticsViewModel>(JsonOptions, cancellationToken)
            ?? new StorageStatisticsViewModel();
    }

    public async Task<IReadOnlyList<UserManagementViewModel>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Get, "ui-api/users");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<UserManagementViewModel>>(JsonOptions, cancellationToken)
            ?? [];
    }

    public async Task<UserManagementViewModel> CreateUserAsync(CreateUserRequestModel requestModel, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Post, "ui-api/users");
        request.Content = JsonContent.Create(requestModel, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<UserManagementViewModel>(JsonOptions, cancellationToken))!;
    }

    public async Task<UserManagementViewModel> UpdateUserAsync(Guid userId, UpdateUserRequestModel requestModel, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Put, $"ui-api/users/{userId}");
        request.Content = JsonContent.Create(requestModel, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<UserManagementViewModel>(JsonOptions, cancellationToken))!;
    }

    public Task BlockUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SendWithoutContentAsync(HttpMethod.Post, $"ui-api/users/{userId}/block", cancellationToken);

    public Task UnblockUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SendWithoutContentAsync(HttpMethod.Post, $"ui-api/users/{userId}/unblock", cancellationToken);

    public Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SendWithoutContentAsync(HttpMethod.Delete, $"ui-api/users/{userId}", cancellationToken);

    public async Task<IReadOnlyList<TenantViewModel>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Get, "ui-api/tenants");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TenantViewModel>>(JsonOptions, cancellationToken)
            ?? [];
    }

    public async Task<TenantViewModel?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Get, $"ui-api/tenants/{tenantId}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TenantViewModel>(JsonOptions, cancellationToken);
    }

    public async Task<TenantViewModel> CreateTenantAsync(TenantUpsertRequest requestModel, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Post, "ui-api/tenants");
        request.Content = JsonContent.Create(requestModel, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TenantViewModel>(JsonOptions, cancellationToken))!;
    }

    public async Task<TenantViewModel> UpdateTenantAsync(Guid tenantId, TenantUpsertRequest requestModel, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Put, $"ui-api/tenants/{tenantId}");
        request.Content = JsonContent.Create(requestModel, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TenantViewModel>(JsonOptions, cancellationToken))!;
    }

    public Task DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        SendWithoutContentAsync(HttpMethod.Delete, $"ui-api/tenants/{tenantId}", cancellationToken);

    public async Task<IReadOnlyList<ApiTokenViewModel>> GetTenantTokensAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Get, $"ui-api/tenants/{tenantId}/api-tokens");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ApiTokenViewModel>>(JsonOptions, cancellationToken)
            ?? [];
    }

    public async Task<CreatedApiTokenViewModel> CreateApiTokenAsync(CreateApiTokenRequestModel requestModel, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Post, "ui-api/api-tokens");
        request.Content = JsonContent.Create(requestModel, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<CreatedApiTokenViewModel>(JsonOptions, cancellationToken))!;
    }

    public async Task<ApiTokenViewModel> UpdateApiTokenAsync(Guid tokenId, UpdateApiTokenRequestModel requestModel, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Put, $"ui-api/api-tokens/{tokenId}");
        request.Content = JsonContent.Create(requestModel, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<ApiTokenViewModel>(JsonOptions, cancellationToken))!;
    }

    public Task DeleteApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default) =>
        SendWithoutContentAsync(HttpMethod.Delete, $"ui-api/api-tokens/{tokenId}", cancellationToken);

    public async Task<IReadOnlyList<BackgroundTaskViewModel>> GetActiveBackgroundTasksAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Get, "ui-api/storage/tasks/active");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<BackgroundTaskViewModel>>(JsonOptions, cancellationToken)
            ?? [];
    }

    public async Task<IReadOnlyList<BackgroundTaskViewModel>> GetCompletedBackgroundTasksAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        using var request = CreateSessionRequest(HttpMethod.Get, $"ui-api/storage/tasks/completed?limit={limit}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<BackgroundTaskViewModel>>(JsonOptions, cancellationToken)
            ?? [];
    }

    public Task QueueConsistencyCheckAsync(CancellationToken cancellationToken = default) =>
        SendWithoutContentAsync(HttpMethod.Post, "ui-api/storage/check-consistency", cancellationToken);

    public Task QueueDeletedTenantCleanupAsync(CancellationToken cancellationToken = default) =>
        SendWithoutContentAsync(HttpMethod.Post, "ui-api/storage/cleanup-deleted-tenants", cancellationToken);

    private async Task SendWithoutContentAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var request = CreateSessionRequest(method, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private HttpRequestMessage CreateSessionRequest(HttpMethod method, string path)
    {
        var serverUrl = _sessionService.GetRequiredServerUrl();
        var request = CreateAbsoluteRequest(method, serverUrl, path);

        if (_sessionService.TryGetApiToken(out var token))
            request.Headers.Add("X-Api-Key", token);
        else if (_sessionService.TryGetSessionCookieHeader(out var cookieHeader))
            request.Headers.Add("Cookie", cookieHeader);
        else
            throw new InvalidOperationException("Sign in before using the admin API.");

        return request;
    }

    private static HttpRequestMessage CreateAbsoluteRequest(HttpMethod method, string serverUrl, string relativePath)
    {
        var baseUri = new Uri(ConnectionSessionService.NormalizeServerUrl(serverUrl)
            ?? throw new InvalidOperationException("Server URL is invalid."));
        return new HttpRequestMessage(method, new Uri(baseUri, relativePath));
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        throw CreateApiException(response.StatusCode, content);
    }

    private static AdminApiException CreateApiException(HttpStatusCode statusCode, string responseBody)
    {
        var error = TryReadJson<ApiErrorResponse>(responseBody);
        var message = !string.IsNullOrWhiteSpace(error?.Message)
            ? error.Message
            : statusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication is required.",
                HttpStatusCode.Forbidden => "The current account is not allowed to perform this action.",
                HttpStatusCode.NotFound => "The requested item was not found.",
                HttpStatusCode.BadRequest => "The request could not be completed.",
                _ => $"Request failed with status {(int)statusCode}."
            };

        return new AdminApiException(statusCode, message);
    }

    private static T? TryReadJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? ReadSessionCookieHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return null;

        var cookies = values
            .Select(value => value.Split(';', 2)[0].Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return cookies.Length == 0 ? null : string.Join("; ", cookies);
    }
}
