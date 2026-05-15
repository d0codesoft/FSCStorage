namespace scp.filestorage
{
    public static class program_utils
    {
        private static Dictionary<string, string> _arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Parses an array of command-line arguments into a dictionary of option names and values.
        /// </summary>
        /// <remarks>Arguments must begin with "--" to be recognized as options. If an option is
        /// immediately followed by a non-option argument, that argument is used as the option's value. Otherwise, the
        /// option's value is set to "true". Option names are normalized to lowercase.</remarks>
        /// <param name="args">An array of command-line arguments, where options are prefixed with "--". Option values may follow their
        /// corresponding option name.</param>
        /// <returns>A dictionary containing option names and their associated values. If an option does not have an explicit
        /// value, its value is set to "true". The dictionary is case-insensitive. Returns an empty dictionary if no
        /// valid options are found.</returns>
        public static void ParseArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (!arg.StartsWith("--"))
                {
                    continue;
                }

                var key = arg[2..].Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string value = "true";

                if (i + 1 < args.Length)
                {
                    var next = args[i + 1];

                    if (!string.IsNullOrWhiteSpace(next) &&
                        !next.StartsWith("--"))
                    {
                        value = next;
                        i++;
                    }
                }

                _arguments[key] = value;
            }
        }

        public static string? GetValueArg(string key)
        {
            if (!_arguments.TryGetValue(key, out var value))
            {
                return null;
            }

            return value;
        }
    }
}
