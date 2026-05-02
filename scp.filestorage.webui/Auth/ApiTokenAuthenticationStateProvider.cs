using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace scp.filestorage.webui.Auth
{
    public sealed class ApiTokenAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

        private readonly ApiTokenStore _tokenStore;

        public ApiTokenAuthenticationStateProvider(ApiTokenStore tokenStore)
        {
            _tokenStore = tokenStore;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _tokenStore.GetTokenAsync();
            return new AuthenticationState(string.IsNullOrWhiteSpace(token)
                ? Anonymous
                : CreateAdminPrincipal());
        }

        public async Task SignInAsync(string token)
        {
            await _tokenStore.SetTokenAsync(token);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CreateAdminPrincipal())));
        }

        public async Task SignOutAsync()
        {
            await _tokenStore.ClearTokenAsync();
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
        }

        private static ClaimsPrincipal CreateAdminPrincipal()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "Admin"),
                new Claim(ClaimTypes.Role, "Admin")
            ], "ApiToken");

            return new ClaimsPrincipal(identity);
        }
    }
}
