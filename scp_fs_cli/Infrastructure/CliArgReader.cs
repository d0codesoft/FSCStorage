namespace scp_fs_cli.Infrastructure
{
    public sealed class CliArgReader
    {
        private readonly string[] _args;
        private readonly HashSet<int> _usedIndexes = [];

        public CliArgReader(string[] args)
        {
            _args = args;
        }

        public string? FirstUnknownOption { get; private set; }

        public string ReadRequiredValue(string name)
        {
            for (var i = 0; i < _args.Length; i++)
            {
                if (_usedIndexes.Contains(i) || _args[i].StartsWith("-", StringComparison.Ordinal))
                    continue;

                _usedIndexes.Add(i);
                return _args[i];
            }

            throw new ArgumentException($"Missing required argument: {name}");
        }

        public string? ReadOptionalValue(string optionName)
        {
            for (var i = 0; i < _args.Length; i++)
            {
                if (!string.Equals(_args[i], optionName, StringComparison.OrdinalIgnoreCase))
                    continue;

                _usedIndexes.Add(i);
                var valueIndex = i + 1;
                if (valueIndex >= _args.Length || _args[valueIndex].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException($"Option {optionName} requires a value.");

                _usedIndexes.Add(valueIndex);
                return _args[valueIndex];
            }

            return null;
        }

        public int ReadInt(string optionName, int defaultValue)
        {
            var value = ReadOptionalValue(optionName);
            if (value is null)
                return defaultValue;

            if (!int.TryParse(value, out var result))
                throw new ArgumentException($"Option {optionName} must be an integer.");

            return result;
        }

        public bool HasUnknownOptions(params string[] knownOptions)
        {
            var known = new HashSet<string>(knownOptions, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < _args.Length; i++)
            {
                if (_usedIndexes.Contains(i) || !_args[i].StartsWith("-", StringComparison.Ordinal))
                    continue;

                if (known.Contains(_args[i]))
                    continue;

                FirstUnknownOption = _args[i];
                return true;
            }

            return false;
        }
    }
}
