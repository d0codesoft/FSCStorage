using scp.filestorage.Data.Dto;
using scp.filestorage.InterfacesService;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.Security;

namespace SCP.StorageFSC.Services
{
    public sealed class ApiTokenService : IApiTokenService
    {
        private readonly IApiTokenRepository _apiTokenRepository;

        public ApiTokenService(IApiTokenRepository apiTokenRepository)
        {
            _apiTokenRepository = apiTokenRepository;
        }

        public async Task<ApiTokenValidationResult?> ValidateAsync(
            string presentedToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(presentedToken))
                return null;

            var hash = TokenHashHelper.ComputeSha256(presentedToken);

            var token = await _apiTokenRepository.GetByHashAsync(hash, cancellationToken);

            if (token is null || !token.IsActive)
                return null;

            if (token.RevokedUtc.HasValue)
                return null;

            if (token.ExpiresUtc.HasValue && token.ExpiresUtc.Value <= DateTime.UtcNow)
                return null;

            var scopes = BuildScopes(token);

            return new ApiTokenValidationResult
            {
                Success = true,
                TokenId = token.Id,
                TenantId = token.TenantId,
                IsAdmin = token.IsAdmin,
                Name = token.Name,
                Roles = Array.Empty<string>(),
                Scopes = scopes
            };
        }

        private static string[] BuildScopes(Data.Models.ApiToken token)
        {
            var scopes = new List<string>(3);

            if (token.CanRead)
            {
                scopes.Add("read");
                scopes.Add("files.read");
            }

            if (token.CanWrite)
            {
                scopes.Add("write");
                scopes.Add("files.write");
            }

            if (token.CanDelete)
            {
                scopes.Add("delete");
                scopes.Add("files.delete");
            }

            if (token.IsAdmin)
                scopes.Add("admin");

            return scopes.ToArray();
        }
    }
}
