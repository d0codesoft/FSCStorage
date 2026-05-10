using System.Text.Json.Serialization;

namespace fsc.mob.client.Api;

public sealed class StorageStatisticsViewModel
{
    public long UsedBytes { get; set; }
    public int StoredFileCount { get; set; }
    public int TenantFileCount { get; set; }
    public int TenantCount { get; set; }
    public IReadOnlyList<LargestFileViewModel> LargestFiles { get; set; } = [];
    public IReadOnlyList<TenantStorageStatisticsViewModel> Tenants { get; set; } = [];
}

public sealed class LargestFileViewModel
{
    public Guid FileGuid { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? ExternalKey { get; set; }
    public Guid TenantId { get; set; }
    public Guid TenantGuid { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedUtc { get; set; }

    [JsonIgnore]
    public string FileSizeDisplay => DisplayFormatting.FormatBytes(FileSize);

    [JsonIgnore]
    public string CreatedUtcDisplay => DisplayFormatting.FormatUtc(CreatedUtc);
}

public sealed class TenantStorageStatisticsViewModel
{
    public Guid TenantId { get; set; }
    public Guid TenantGuid { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int FileCount { get; set; }
    public long UsedBytes { get; set; }

    [JsonIgnore]
    public string UsedBytesDisplay => DisplayFormatting.FormatBytes(UsedBytes);
}

public sealed class TenantViewModel
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantGuid { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    [JsonIgnore]
    public string StatusLabel => IsActive ? "Active" : "Inactive";

    [JsonIgnore]
    public string CreatedUtcDisplay => DisplayFormatting.FormatUtc(CreatedUtc);
}

public sealed class TenantUpsertRequest
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class ApiTokenViewModel
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }

    [JsonIgnore]
    public string ScopesLabel => string.Join(", ", GetScopes().DefaultIfEmpty("none"));

    [JsonIgnore]
    public string StatusLabel => IsActive ? "Active" : "Inactive";

    private IEnumerable<string> GetScopes()
    {
        if (CanRead)
            yield return "read";
        if (CanWrite)
            yield return "write";
        if (CanDelete)
            yield return "delete";
        if (IsAdmin)
            yield return "admin";
    }
}

public sealed class CreateApiTokenRequestModel
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool CanRead { get; set; } = true;
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime? ExpiresUtc { get; set; }
}

public sealed class UpdateApiTokenRequestModel
{
    public string Name { get; set; } = string.Empty;
    public bool CanRead { get; set; } = true;
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresUtc { get; set; }
}

public sealed class CreatedApiTokenViewModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public bool IsAdmin { get; set; }
    public DateTime? ExpiresUtc { get; set; }
}

public sealed class UserApiTokenViewModel
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }

    [JsonIgnore]
    public string ScopesLabel => string.Join(", ", GetScopes().DefaultIfEmpty("none"));

    [JsonIgnore]
    public string StatusLabel => IsActive ? "Active" : "Inactive";

    private IEnumerable<string> GetScopes()
    {
        if (CanRead)
            yield return "read";
        if (CanWrite)
            yield return "write";
        if (CanDelete)
            yield return "delete";
        if (IsAdmin)
            yield return "admin";
    }
}

public sealed class UserManagementViewModel
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public IReadOnlyList<TenantViewModel> Tenants { get; set; } = [];
    public IReadOnlyList<UserApiTokenViewModel> ApiTokens { get; set; } = [];

    [JsonIgnore]
    public string RoleLabel => IsAdmin ? "Administrator" : "User";

    [JsonIgnore]
    public string StatusLabel => IsLocked ? "Locked" : IsActive ? "Active" : "Inactive";

    [JsonIgnore]
    public int TenantCount => Tenants.Count;

    [JsonIgnore]
    public int ApiTokenCount => ApiTokens.Count;

    [JsonIgnore]
    public string CreatedUtcDisplay => DisplayFormatting.FormatUtc(CreatedUtc);
}

public sealed class CreateUserRequestModel
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; }
}

public sealed class UpdateUserRequestModel
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; }
}

public sealed class BackgroundTaskViewModel
{
    public Guid TaskId { get; set; }
    public short Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public Guid? UploadId { get; set; }
    public DateTime QueuedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultSummary { get; set; }

    [JsonIgnore]
    public string TimelineLabel => $"Queued {DisplayFormatting.FormatUtc(QueuedAtUtc)}";

    [JsonIgnore]
    public string StatusSummary => string.IsNullOrWhiteSpace(ResultSummary) ? StatusName : $"{StatusName}: {ResultSummary}";
}

public sealed class AuthSignInResult
{
    public bool RequiresTwoFactor { get; init; }
    public string TwoFactorMethod { get; init; } = string.Empty;
    public string? ChallengeToken { get; init; }
    public DateTime? ChallengeExpiresUtc { get; init; }
    public MeResponse? User { get; init; }
    public string? SessionCookieHeader { get; init; }
}

public sealed class LoginRequest
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("remember")]
    public bool Remember { get; set; }

    [JsonPropertyName("twoFactorCode")]
    public string? TwoFactorCode { get; set; }
}

public sealed class VerifyTwoFactorLoginRequest
{
    [JsonPropertyName("challengeToken")]
    public string ChallengeToken { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("remember")]
    public bool Remember { get; set; }
}

public sealed class LoginChallengeResponse
{
    [JsonPropertyName("requiresTwoFactor")]
    public bool RequiresTwoFactor { get; set; }

    [JsonPropertyName("twoFactorMethod")]
    public string TwoFactorMethod { get; set; } = string.Empty;

    [JsonPropertyName("challengeToken")]
    public string? ChallengeToken { get; set; }

    [JsonPropertyName("challengeExpiresUtc")]
    public DateTime? ChallengeExpiresUtc { get; set; }
}

public sealed class MeResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; set; }

    [JsonPropertyName("tenantId")]
    public Guid? TenantId { get; set; }

    [JsonPropertyName("scopes")]
    public string[] Scopes { get; set; } = [];
}

public sealed class ApiErrorResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class UserTokenGroup
{
    public string TenantName { get; init; } = string.Empty;
    public IReadOnlyList<UserApiTokenViewModel> Tokens { get; init; } = [];
}

public sealed class UserOption
{
    public Guid UserId { get; init; }
    public string Label { get; init; } = string.Empty;
}

public static class DisplayFormatting
{
    public static string FormatUtc(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")
            : "Never";
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var index = 0;

        while (size >= 1024 && index < units.Length - 1)
        {
            size /= 1024;
            index++;
        }

        return $"{size:0.##} {units[index]}";
    }
}
