using SCP.StorageFSC.Data.Dto;

namespace SCP.StorageFSC.InterfacesService
{
    public interface ITenantStorageService
    {
        Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
        Task<TenantDto?> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default);
        Task<TenantDto?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<TenantDto?> GetTenantByGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserManagementDto>> GetUsersAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserTenantsDto>> GetUsersWithTenantsAsync(CancellationToken cancellationToken = default);
        Task<UserManagementDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
        Task<UserManagementDto?> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
        Task<bool> SetUserBlockedAsync(Guid userId, bool isBlocked, CancellationToken cancellationToken = default);
        Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DisableTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<CreatedApiTokenResult> CreateApiTokenAsync(CreateApiTokenRequest request, CancellationToken cancellationToken = default);
        Task<ApiTokenDto?> UpdateApiTokenAsync(Guid tokenId, UpdateApiTokenRequest request, CancellationToken cancellationToken = default);
        Task<ApiTokenDto?> GetApiTokenByIdAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiTokenDto>> GetTenantTokensAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> RevokeApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<bool> DeleteApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<CreatedApiTokenResult?> RotateApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
    }
}
