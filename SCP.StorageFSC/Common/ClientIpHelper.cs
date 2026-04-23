using System.Net;

namespace scp.filestorage.Common
{
    public static class ClientIpHelper
    {
        public static ClientIpInfo GetClientIp(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var remoteIp = context.Connection.RemoteIpAddress;
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            var realIp = context.Request.Headers["X-Real-IP"].ToString();

            IPAddress? clientIp = null;
            string source = "RemoteIpAddress";

            // X-Forwarded-For
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();

                if (TryParseIp(firstIp, out clientIp))
                {
                    source = "X-Forwarded-For";
                }
            }

            // X-Real-IP
            if (clientIp is null && !string.IsNullOrWhiteSpace(realIp))
            {
                if (TryParseIp(realIp, out clientIp))
                {
                    source = "X-Real-IP";
                }
            }

            // RemoteIpAddress
            if (clientIp is null && remoteIp is not null)
            {
                clientIp = remoteIp;
            }

            // IPv4 mapped IPv6 ::ffff:1.2.3.4
            if (clientIp is not null && clientIp.IsIPv4MappedToIPv6)
            {
                clientIp = clientIp.MapToIPv4();
            }

            return new ClientIpInfo
            {
                IpAddress = clientIp,
                Ip = clientIp?.ToString() ?? string.Empty,
                IsLocal = clientIp is not null && IPAddress.IsLoopback(clientIp),
                IsIPv4 = clientIp?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork,
                IsIPv6 = clientIp?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6,
                Source = source,
                ForwardedForRaw = forwardedFor,
                RealIpRaw = realIp
            };
        }

        private static bool TryParseIp(string? value, out IPAddress? ip)
        {
            ip = null;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            return IPAddress.TryParse(value, out ip);
        }
    }

    public sealed class ClientIpInfo
    {
        public IPAddress? IpAddress { get; init; }

        public string Ip { get; init; } = string.Empty;

        public bool IsLocal { get; init; }

        public bool IsIPv4 { get; init; }

        public bool IsIPv6 { get; init; }

        public string Source { get; init; } = string.Empty;

        public string ForwardedForRaw { get; init; } = string.Empty;

        public string RealIpRaw { get; init; } = string.Empty;

        public override string ToString()
        {
            var type =
                IsIPv4 ? "IPv4" :
                IsIPv6 ? "IPv6" :
                "Unknown";

            var local = IsLocal ? "Local" : "Remote";

            return $"IP={Ip}, Type={type}, Scope={local}, Source={Source}";
        }
    }
}
