using System.Net.Http.Headers;

namespace scp.filestorage.webui.Auth
{
    public sealed class ApiTokenAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly ApiTokenStore _tokenStore;

        public ApiTokenAuthorizationMessageHandler(ApiTokenStore tokenStore)
        {
            _tokenStore = tokenStore;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var token = await _tokenStore.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Remove("X-Api-Key");
                request.Headers.Add("X-Api-Key", token);
            }

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
