namespace SCP.StorageFSC.Data.Models
{
    public sealed class UserAuthenticationAuditLog : EntityBase
    {
        public Guid? UserId { get; set; }

        public string? UserName { get; set; }

        public string? Login { get; set; }

        public string EventType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool IsSuccess { get; set; }

        public string? FailureReason { get; set; }

        public string ClientIp { get; set; } = string.Empty;

        public string IpSource { get; set; } = string.Empty;

        public string? ForwardedForRaw { get; set; }

        public string? RealIpRaw { get; set; }

        public string RequestPath { get; set; } = string.Empty;

        public string? UserAgent { get; set; }

        public override string ToString()
        {
            var status = IsSuccess ? "Success" : "Failed";
            var user = UserName ?? UserId?.ToString() ?? Login ?? "Unknown";
            var failure = FailureReason ?? "None";

            return $"Event={EventType}, Status={status}, User={user}, IP={ClientIp}, Path={RequestPath}, Failure={failure}";
        }
    }
}
