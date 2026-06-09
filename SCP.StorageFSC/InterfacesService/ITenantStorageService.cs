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
        Task<IReadOnlyList<StoredTenantFileDto>> GetTenantFilesAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserManagementDto>> GetUsersAsync(CancellationToken cancellationToken = default);
        Task<UserManagementDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserTenantsDto>> GetUsersWithTenantsAsync(CancellationToken cancellationToken = default);
        Task<UserManagementDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
        Task<UserManagementDto?> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
        Task<UserManagementDto?> UpdateUserProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default);
        Task<bool> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> LockUserAsync(Guid userId, DateTime lockedUntilUtc, CancellationToken cancellationToken = default);
        Task<bool> UnlockUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> ResetFailedLoginCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> SetUserBlockedAsync(Guid userId, bool isBlocked, CancellationToken cancellationToken = default);
        Task<bool> ResetUserPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken = default);
        Task<bool> ExpireUserPasswordAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> ChangeUserEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default);
        Task<bool> SetUserEmailConfirmedAsync(Guid userId, bool confirmed, CancellationToken cancellationToken = default);
        Task<bool> ChangeUserPhoneAsync(Guid userId, string phoneNumber, CancellationToken cancellationToken = default);
        Task<bool> SetUserPhoneConfirmedAsync(Guid userId, bool confirmed, CancellationToken cancellationToken = default);
        Task<UserTwoFactorStatusDto?> GetUserTwoFactorStatusAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> EnableUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DisableUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> SetUserPreferredTwoFactorMethodAsync(Guid userId, SetPreferredTwoFactorMethodRequest request, CancellationToken cancellationToken = default);
        Task<bool> SetUserTwoFactorRequiredForEveryLoginAsync(Guid userId, bool required, CancellationToken cancellationToken = default);
        Task<bool> ResetUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<string[]> RegenerateUserRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> RefreshUserSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserSessionDto>?> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserLoginHistoryDto>?> GetUserLoginHistoryAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserSecurityEventDto>?> GetUserSecurityEventsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> IsUserNameUniqueAsync(string name, Guid? excludingUserId = null, CancellationToken cancellationToken = default);
        Task<bool> IsUserEmailUniqueAsync(string email, Guid? excludingUserId = null, CancellationToken cancellationToken = default);
        Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DisableTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<CreatedApiTokenResult> CreateApiTokenAsync(CreateApiTokenRequest request, CancellationToken cancellationToken = default);
        Task<CreatedApiTokenResult?> CreateTenantApiTokenAsync(Guid tenantId, CreateTenantApiTokenRequest request, CancellationToken cancellationToken = default);
        Task<ApiTokenDto?> UpdateApiTokenAsync(Guid tokenId, UpdateApiTokenRequest request, CancellationToken cancellationToken = default);
        Task<ApiTokenDto?> GetApiTokenByIdAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiTokenDto>> GetTenantTokensAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> RevokeApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<bool> DeleteApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task<bool> DeleteTenantApiTokenAsync(Guid tenantId, Guid tokenId, CancellationToken cancellationToken = default);
        Task<CreatedApiTokenResult?> RotateApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
    }
}
