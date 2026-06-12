namespace SCP.StorageFSC.Data.Dto
{
    public sealed class UserAuthenticationNotificationDto
    {
        public Guid Id { get; set; }
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
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class UserAuthenticationNotificationPageDto
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public IReadOnlyList<UserAuthenticationNotificationDto> Items { get; set; } = [];
    }
}