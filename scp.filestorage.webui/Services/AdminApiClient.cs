using System.Net;
using System.Net.Http.Json;
using scp.filestorage.webui.Models;

namespace scp.filestorage.webui.Services
{
    public sealed class AdminApiClient
    {
        private readonly HttpClient _httpClient;

        public AdminApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<StorageStatisticsViewModel> GetStorageStatisticsAsync(
            int largestFilesLimit = 25,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(
                $"ui-api/storage/statistics?largestFilesLimit={largestFilesLimit}",
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<StorageStatisticsViewModel>(
                cancellationToken: cancellationToken)
                ?? new StorageStatisticsViewModel();
        }

        public async Task<IReadOnlyList<TenantViewModel>> GetTenantsAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync("ui-api/tenants", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<TenantViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task<TenantViewModel?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"ui-api/tenants/{tenantId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<TenantViewModel>(
                cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyList<UserTenantsViewModel>> GetUsersWithTenantsAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync("ui-api/users/tenants", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<UserTenantsViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task<IReadOnlyList<UserManagementViewModel>> GetUsersAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync("ui-api/users", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<UserManagementViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task<UserManagementViewModel?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"ui-api/users/{userId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<UserManagementViewModel>(
                cancellationToken: cancellationToken);
        }

        public async Task<UserManagementViewModel> CreateUserAsync(
            CreateUserRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("ui-api/users", request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<UserManagementViewModel>(cancellationToken: cancellationToken))!;
        }

        public async Task<UserManagementViewModel> UpdateUserAsync(
            Guid userId,
            UpdateUserRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"ui-api/users/{userId}", request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<UserManagementViewModel>(cancellationToken: cancellationToken))!;
        }

        public async Task<UserManagementViewModel> UpdateUserProfileAsync(
            Guid userId,
            UpdateUserProfileRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"ui-api/users/{userId}", request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<UserManagementViewModel>(cancellationToken: cancellationToken))!;
        }

        public Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => PostUserActionAsync(userId, "activate", cancellationToken);

        public Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => PostUserActionAsync(userId, "deactivate", cancellationToken);

        public async Task BlockUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsync($"ui-api/users/{userId}/block", null, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task UnblockUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsync($"ui-api/users/{userId}/unblock", null, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"ui-api/users/{userId}", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task ResetUserPasswordAsync(
            Guid userId,
            ResetUserPasswordRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"ui-api/users/{userId}/password/reset",
                request,
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);
        }

        public Task ExpireUserPasswordAsync(Guid userId, CancellationToken cancellationToken = default)
            => PostUserActionAsync(userId, "password/expire", cancellationToken);

        public async Task<UserTwoFactorStatusViewModel?> GetUserTwoFactorStatusAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"ui-api/users/{userId}/2fa/status", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<UserTwoFactorStatusViewModel>(
                cancellationToken: cancellationToken);
        }

        public Task EnableUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default)
            => PostUserActionAsync(userId, "2fa/enable", cancellationToken);

        public Task DisableUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default)
            => PostUserActionAsync(userId, "2fa/disable", cancellationToken);

        public async Task<AuthenticatorSetupViewModel> SetupUserAuthenticatorAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsync(
                $"ui-api/users/{userId}/2fa/setup-authenticator",
                null,
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<AuthenticatorSetupViewModel>(
                cancellationToken: cancellationToken)
                ?? new AuthenticatorSetupViewModel { Status = TwoFactorSetupStatus.UserNotFound };
        }

        public async Task ConfirmUserAuthenticatorAsync(
            Guid userId,
            string code,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"ui-api/users/{userId}/2fa/confirm-authenticator",
                new ConfirmAuthenticatorRequestModel { Code = code },
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task SetUserPreferredTwoFactorMethodAsync(
            Guid userId,
            TwoFactorMethodType methodType,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"ui-api/users/{userId}/2fa/set-preferred-method",
                new SetPreferredTwoFactorMethodRequestModel { PreferredTwoFactorMethod = methodType },
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task SetUserTwoFactorRequiredAsync(
            Guid userId,
            bool required,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"ui-api/users/{userId}/2fa/set-required-for-every-login",
                new SetTwoFactorRequiredRequestModel { Required = required },
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);
        }

        public Task ResetUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default)
            => PostUserActionAsync(userId, "2fa/reset", cancellationToken);

        public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsync(
                $"ui-api/users/{userId}/2fa/recovery-codes/regenerate",
                null,
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<RecoveryCodesResponse>(
                cancellationToken: cancellationToken);

            return result?.RecoveryCodes ?? [];
        }

        public Task RefreshUserSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default)
            => PostUserActionAsync(userId, "security-stamp/refresh", cancellationToken);

        public Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
            => PostUserActionAsync(userId, "sessions/revoke-all", cancellationToken);

        public async Task<IReadOnlyList<UserLoginHistoryViewModel>> GetUserLoginHistoryAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"ui-api/users/{userId}/login-history", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<UserLoginHistoryViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task<IReadOnlyList<UserSecurityEventViewModel>> GetUserSecurityEventsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"ui-api/users/{userId}/security-events", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<UserSecurityEventViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task ChangePasswordAsync(
            ChangePasswordRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("auth/change-password", request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task SendUserEmailConfirmationAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsync(
                $"ui-api/users/{userId}/email/send-confirmation",
                null,
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task ConfirmUserEmailAsync(
            Guid userId,
            string code,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"ui-api/users/{userId}/email/confirm",
                new ConfirmUserEmailRequestModel { Code = code },
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task<TenantViewModel> CreateTenantAsync(
            TenantUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("ui-api/tenants", request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<TenantViewModel>(cancellationToken: cancellationToken))!;
        }

        public async Task<TenantViewModel> UpdateTenantAsync(
            Guid tenantId,
            TenantUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"ui-api/tenants/{tenantId}", request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<TenantViewModel>(cancellationToken: cancellationToken))!;
        }

        public async Task DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"ui-api/tenants/{tenantId}", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task<IReadOnlyList<ApiTokenViewModel>> GetTenantTokensAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"ui-api/tenants/{tenantId}/api-tokens", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<ApiTokenViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task<IReadOnlyList<StoredTenantFileViewModel>> GetTenantFilesAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"ui-api/tenants/{tenantId}/files", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<StoredTenantFileViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task<CreatedApiTokenViewModel> CreateApiTokenAsync(
            CreateApiTokenRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("ui-api/api-tokens", request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<CreatedApiTokenViewModel>(cancellationToken: cancellationToken))!;
        }

        public async Task<CreatedApiTokenViewModel> CreateTenantApiTokenAsync(
            Guid tenantId,
            CreateTenantApiTokenRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"ui-api/tenants/{tenantId}/api-tokens",
                request,
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<CreatedApiTokenViewModel>(cancellationToken: cancellationToken))!;
        }

        public async Task<ApiTokenViewModel> UpdateApiTokenAsync(
            Guid tokenId,
            UpdateApiTokenRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"ui-api/api-tokens/{tokenId}", request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<ApiTokenViewModel>(cancellationToken: cancellationToken))!;
        }

        public async Task DeleteApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"ui-api/api-tokens/{tokenId}", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task DeleteTenantApiTokenAsync(
            Guid tenantId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync(
                $"ui-api/tenants/{tenantId}/api-tokens/{tokenId}",
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task<IReadOnlyList<BackgroundTaskViewModel>> GetActiveBackgroundTasksAsync(
            CancellationToken cancellationToken = default)
        {
            return await GetBackgroundTasksAsync("ui-api/storage/tasks/active", cancellationToken);
        }

        public async Task<IReadOnlyList<BackgroundTaskViewModel>> GetCompletedBackgroundTasksAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            return await GetBackgroundTasksAsync(
                $"ui-api/storage/tasks/completed?limit={limit}",
                cancellationToken);
        }

        public async Task<BackgroundTaskViewModel?> GetBackgroundTaskAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"ui-api/storage/tasks/{taskId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<BackgroundTaskViewModel>(
                cancellationToken: cancellationToken);
        }

        public async Task QueueConsistencyCheckAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsync("ui-api/storage/check-consistency", null, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task QueueDeletedTenantCleanupAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsync("ui-api/storage/cleanup-deleted-tenants", null, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        public async Task<IReadOnlyList<SystemSettingViewModel>> GetSystemSettingsAsync(
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync("ui-api/system-settings", cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<SystemSettingViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task<SystemSettingViewModel?> GetSystemSettingAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(
                $"ui-api/system-settings/{Uri.EscapeDataString(name)}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<SystemSettingViewModel>(
                cancellationToken: cancellationToken);
        }

        public async Task<SystemSettingViewModel> UpdateSystemSettingAsync(
            string name,
            UpdateSystemSettingRequestModel request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"ui-api/system-settings/{Uri.EscapeDataString(name)}",
                request,
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<SystemSettingViewModel>(
                cancellationToken: cancellationToken))!;
        }

        private async Task<IReadOnlyList<BackgroundTaskViewModel>> GetBackgroundTasksAsync(
            string url,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<BackgroundTaskViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        private async Task PostUserActionAsync(Guid userId, string action, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsync($"ui-api/users/{userId}/{action}", null, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        private sealed class RecoveryCodesResponse
        {
            public string[] RecoveryCodes { get; set; } = [];
        }

        private static async Task EnsureSuccessAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Sign in is required.",
                HttpStatusCode.Forbidden => "The current user is not allowed to access this data.",
                HttpStatusCode.NotFound => "Requested item was not found.",
                _ => string.IsNullOrWhiteSpace(body)
                    ? $"Request failed with status {(int)response.StatusCode}."
                    : body
            };

            throw new InvalidOperationException(message);
        }
    }
}
