namespace fsc_adm_cli.Infrastructure
{
    public static class CliArgReader
    {
        public static string? GetValue(string[] args, params string[] keys)
        {
            for (var i = 0; i < args.Length; i++)
            {
                foreach (var key in keys)
                {
                    if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        return args[i + 1];
                }
            }

            return null;
        }

        public static bool HasFlag(string[] args, params string[] keys)
        {
            foreach (var arg in args)
            {
                foreach (var key in keys)
                {
                    if (string.Equals(arg, key, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public static Guid? GetGuid(string[] args, params string[] keys)
        {
            var value = GetValue(args, keys);
            return Guid.TryParse(value, out var guid) ? guid : null;
        }

        public static int? GetInt32(string[] args, params string[] keys)
        {
            var value = GetValue(args, keys);
            return int.TryParse(value, out var result) ? result : null;
        }

        public static DateTimeOffset? GetDateTimeOffset(string[] args, params string[] keys)
        {
            var value = GetValue(args, keys);
            return DateTimeOffset.TryParse(value, out var result) ? result : null;
        }
    }
}
