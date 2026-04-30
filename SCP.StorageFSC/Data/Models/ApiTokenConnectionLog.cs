namespace SCP.StorageFSC.Data.Models
{
    /// <summary>
    /// Audit log entry for API token connections.
    /// </summary>
    public sealed class ApiTokenConnectionLog : EntityBase
    {
        /// <summary>
        /// API token identifier.
        /// </summary>
        public Guid? ApiTokenId { get; set; }

        /// <summary>
        /// Token name snapshot at the time of connection.
        /// </summary>
        public string TokenName { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier resolved for the request.
        /// </summary>
        public Guid? TenantId { get; set; }

        /// <summary>
        /// External tenant GUID resolved for the request.
        /// </summary>
        public Guid? ExternalTenantId { get; set; }

        /// <summary>
        /// Tenant name snapshot at the time of connection.
        /// </summary>
        public string TenantName { get; set; } = string.Empty;

        /// <summary>
        /// Indicates that authentication or authorization succeeded.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Error message for failed authentication or authorization.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Indicates that the token was administrative.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Resolved client IP address.
        /// </summary>
        public string ClientIp { get; set; } = string.Empty;

        /// <summary>
        /// IP source, for example X-Forwarded-For, X-Real-IP, or RemoteIpAddress.
        /// </summary>
        public string IpSource { get; set; } = string.Empty;

        /// <summary>
        /// Raw X-Forwarded-For header value.
        /// </summary>
        public string? ForwardedForRaw { get; set; }

        /// <summary>
        /// Raw X-Real-IP header value.
        /// </summary>
        public string? RealIpRaw { get; set; }

        /// <summary>
        /// Request path for which the connection was recorded.
        /// </summary>
        public string RequestPath { get; set; } = string.Empty;

        /// <summary>
        /// Optional user agent snapshot.
        /// </summary>
        public string? UserAgent { get; set; }

        public override string ToString()
        {
            var status = IsSuccess ? "Success" : "Failed";
            var admin = IsAdmin ? "Admin" : "Tenant";
            var token = string.IsNullOrWhiteSpace(TokenName) ? ApiTokenId?.ToString() ?? "Unknown" : TokenName;
            var tenant = string.IsNullOrWhiteSpace(TenantName)
                ? ExternalTenantId?.ToString() ?? TenantId?.ToString() ?? "None"
                : TenantName;
            var error = ErrorMessage ?? "None";

            return $"Status={status}, Token={token}, Tenant={tenant}, Access={admin}, IP={ClientIp}, Path={RequestPath}, Error={error}";
        }
    }
}
