using Dapper;
using System.Data;

namespace scp.filestorage.Data.Handlers
{
    public sealed class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.Value = value.ToUniversalTime()
                .ToString("O"); // ISO8601
        }

        public override DateTime Parse(object value)
        {
            if (value is string s)
            {
                var parsed = DateTime.Parse(
                    s,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind);

                return parsed.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    : parsed.ToUniversalTime();
            }

            return DateTime.SpecifyKind(
                Convert.ToDateTime(value),
                DateTimeKind.Utc);
        }
    }
}
