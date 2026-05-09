using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace scp.filestorage.webui.Auth
{
    public sealed class ApiTokenAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

        private readonly HttpClient _httpClient;

        public ApiTokenAuthenticationStateProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("auth/me");
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return new AuthenticationState(Anonymous);

                response.EnsureSuccessStatusCode();

                var user = await response.Content.ReadFromJsonAsync<MeResponse>();
                return new AuthenticationState(user is null ? Anonymous : CreatePrincipal(user));
            }
            catch
            {
                return new AuthenticationState(Anonymous);
            }
        }

        public async Task<SignInResult> SignInAsync(
            string login,
            string password,
            bool remember,
            string? twoFactorCode = null)
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "auth/login",
                new LoginRequest(login, password, remember, twoFactorCode));

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var authenticationError = TryGetAuthenticationErrorMessage(response.StatusCode, responseBody);
                if (!string.IsNullOrWhiteSpace(authenticationError))
                    return SignInResult.Failed(authenticationError);

                throw CreateAuthenticationException(response.StatusCode, responseBody);
            }

            var challenge = TryReadJson<LoginChallengeResponse>(responseBody);
            if (challenge?.RequiresTwoFactor == true)
                return SignInResult.TwoFactorRequired(challenge);

            var user = TryReadJson<MeResponse>(responseBody)
                ?? throw new InvalidOperationException("Sign in response did not include user information.");

            NotifySignedIn(user);
            return SignInResult.SignedIn(user);
        }

        public async Task<MeResponse> VerifyTwoFactorAsync(
            string challengeToken,
            string code,
            bool remember)
        {
            return await VerifyTwoFactorCoreAsync(
                "auth/two-factor",
                challengeToken,
                code,
                remember);
        }

        public async Task<MeResponse> VerifyRecoveryCodeAsync(
            string challengeToken,
            string code,
            bool remember)
        {
            return await VerifyTwoFactorCoreAsync(
                "auth/recovery-code",
                challengeToken,
                code,
                remember);
        }

        public async Task SignOutAsync()
        {
            using var response = await _httpClient.PostAsync("auth/logout", null);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                response.EnsureSuccessStatusCode();

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
        }

        private static ClaimsPrincipal CreatePrincipal(MeResponse user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.Name),
                new("auth_type", "web_user")
            };

            if (user.IsAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

            if (user.TenantId.HasValue)
                claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

            foreach (var scope in user.Scopes)
                claims.Add(new Claim("scope", scope));

            var identity = new ClaimsIdentity(claims, "Cookie");
            return new ClaimsPrincipal(identity);
        }

        private async Task<MeResponse> VerifyTwoFactorCoreAsync(
            string url,
            string challengeToken,
            string code,
            bool remember)
        {
            using var response = await _httpClient.PostAsJsonAsync(
                url,
                new VerifyTwoFactorLoginRequest(challengeToken, code, remember));

            await EnsureSuccessAsync(response);

            var responseBody = await response.Content.ReadAsStringAsync();
            var user = TryReadJson<MeResponse>(responseBody)
                ?? throw new InvalidOperationException("Two-factor response did not include user information.");

            NotifySignedIn(user);
            return user;
        }

        private void NotifySignedIn(MeResponse user)
        {
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CreatePrincipal(user))));
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var responseBody = await response.Content.ReadAsStringAsync();
            throw CreateAuthenticationException(response.StatusCode, responseBody);
        }

        private static InvalidOperationException CreateAuthenticationException(HttpStatusCode statusCode, string responseBody)
        {
            return new InvalidOperationException(TryGetAuthenticationErrorMessage(statusCode, responseBody) switch
            {
                { Length: > 0 } message => message,
                _ => $"Authentication request failed with status {(int)statusCode}."
            });
        }

        private static string? TryGetAuthenticationErrorMessage(HttpStatusCode statusCode, string responseBody)
        {
            var error = TryReadJson<ApiErrorResponse>(responseBody);
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message;

            return statusCode switch
            {
                HttpStatusCode.Unauthorized => "Login, password, or verification code is invalid.",
                HttpStatusCode.Forbidden => "The current user is not allowed to sign in.",
                HttpStatusCode.BadRequest => "Check the entered sign-in details.",
                _ => null
            };
        }

        private static T? TryReadJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        private sealed record LoginRequest(
            [property: JsonPropertyName("login")] string Login,
            [property: JsonPropertyName("password")] string Password,
            [property: JsonPropertyName("remember")] bool Remember,
            [property: JsonPropertyName("twoFactorCode")] string? TwoFactorCode);

        private sealed record VerifyTwoFactorLoginRequest(
            [property: JsonPropertyName("challengeToken")] string ChallengeToken,
            [property: JsonPropertyName("code")] string Code,
            [property: JsonPropertyName("remember")] bool Remember);

        public sealed record LoginChallengeResponse(
            [property: JsonPropertyName("requiresTwoFactor")] bool RequiresTwoFactor,
            [property: JsonPropertyName("twoFactorMethod")] string TwoFactorMethod,
            [property: JsonPropertyName("challengeToken")] string? ChallengeToken,
            [property: JsonPropertyName("challengeExpiresUtc")] DateTime? ChallengeExpiresUtc);

        public sealed record MeResponse(
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("isAdmin")] bool IsAdmin,
            [property: JsonPropertyName("tenantId")] Guid? TenantId,
            [property: JsonPropertyName("scopes")] string[] Scopes);

        public sealed record SignInResult(
            bool RequiresTwoFactor,
            LoginChallengeResponse? Challenge,
            MeResponse? User,
            string? ErrorMessage)
        {
            public static SignInResult SignedIn(MeResponse user)
            {
                return new SignInResult(false, null, user, null);
            }

            public static SignInResult TwoFactorRequired(LoginChallengeResponse challenge)
            {
                return new SignInResult(true, challenge, null, null);
            }

            public static SignInResult Failed(string errorMessage)
            {
                return new SignInResult(false, null, null, errorMessage);
            }
        }

        private sealed record ApiErrorResponse(
            [property: JsonPropertyName("success")] bool Success,
            [property: JsonPropertyName("errorCode")] string ErrorCode,
            [property: JsonPropertyName("message")] string Message,
            [property: JsonPropertyName("traceId")] string? TraceId);
    }
}
