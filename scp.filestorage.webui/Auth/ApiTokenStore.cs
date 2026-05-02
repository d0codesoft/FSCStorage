using Microsoft.JSInterop;

namespace scp.filestorage.webui.Auth
{
    public sealed class ApiTokenStore
    {
        private const string TokenKey = "scp.filestorage.webui.apiToken";

        private readonly IJSRuntime _jsRuntime;
        private string? _cachedToken;
        private bool _isLoaded;

        public ApiTokenStore(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async ValueTask<string?> GetTokenAsync()
        {
            if (_isLoaded)
                return _cachedToken;

            _cachedToken = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            _isLoaded = true;
            return _cachedToken;
        }

        public async ValueTask SetTokenAsync(string token)
        {
            _cachedToken = token;
            _isLoaded = true;
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
        }

        public async ValueTask ClearTokenAsync()
        {
            _cachedToken = null;
            _isLoaded = true;
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        }
    }
}
