using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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

        public async Task SignInAsync(string token)
        {
            var response = await _httpClient.PostAsJsonAsync("auth/login", new LoginRequest(token));
            response.EnsureSuccessStatusCode();

            var user = await response.Content.ReadFromJsonAsync<MeResponse>()
                ?? new MeResponse("Admin", true, null, []);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CreatePrincipal(user))));
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
                new(ClaimTypes.Name, user.Name)
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

        private sealed record LoginRequest(
            [property: JsonPropertyName("apiToken")] string ApiToken,
            [property: JsonPropertyName("remember")] bool Remember = false);

        private sealed record MeResponse(
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("isAdmin")] bool IsAdmin,
            [property: JsonPropertyName("tenantId")] Guid? TenantId,
            [property: JsonPropertyName("scopes")] string[] Scopes);
    }
}
