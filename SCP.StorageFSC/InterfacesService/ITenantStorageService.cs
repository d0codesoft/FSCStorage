using SCP.StorageFSC.Data.Dto;

namespace SCP.StorageFSC.InterfacesService
{
    public interface ITenantStorageService
    {
        Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
        Task<TenantDto?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<TenantDto?> GetTenantByGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default);
        Task<bool> DisableTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<CreatedApiTokenResult> CreateApiTokenAsync(CreateApiTokenRequest request, CancellationToken cancellationToken = default);
        Task<ApiTokenDto?> GetApiTokenByIdAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiTokenDto>> GetTenantTokensAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> RevokeApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<bool> DeleteApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<CreatedApiTokenResult?> RotateApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
    }
}
