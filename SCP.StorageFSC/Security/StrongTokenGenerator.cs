using System.Security.Cryptography;

namespace scp.filestorage.Security
{
    /// <summary>
    /// Cryptographically secure token generator.
    /// Suitable for API keys, access tokens, one-time tokens, and so on.
    /// </summary>
    public static class StrongTokenGenerator
    {
        public const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
        public const string Digits = "0123456789";

        /// <summary>
        /// URL-safe special characters. This is usually enough for an API token.
        /// </summary>
        public const string UrlSafeSpecial = "";

        /// <summary>
        /// A wider set of special characters.
        /// Use only if it is explicitly known that such characters are allowed
        /// in HTTP headers, configuration, JSON, URLs, CLI, and similar contexts.
        /// </summary>
        public const string ExtendedSpecial = "_-!.~";

        public const string DefaultUrlSafeAlphabet = Uppercase + Lowercase + Digits + UrlSafeSpecial;

        /// <summary>
        /// Generates a token from the specified alphabet.
        /// </summary>
        public static string Generate(int length = 40, string? alphabet = null)
        {
            alphabet ??= DefaultUrlSafeAlphabet;

            ValidateLength(length);
            ValidateAlphabet(alphabet);

            var chars = new char[length];

            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }

            return new string(chars);
        }

        /// <summary>
        /// Generates a strong token with the required presence of:
        /// - at least one uppercase letter
        /// - at least one lowercase letter
        /// - at least one digit
        /// - at least one special character
        /// </summary>
        public static string GenerateStrong(
            int length = 40,
            bool requireUppercase = true,
            bool requireLowercase = true,
            bool requireDigit = true,
            bool requireSpecial = true,
            string specialAlphabet = UrlSafeSpecial)
        {
            ValidateLength(length);
            if (requireSpecial)
                ValidateAlphabet(specialAlphabet, nameof(specialAlphabet));

            var requiredGroupsCount =
                (requireUppercase ? 1 : 0) +
                (requireLowercase ? 1 : 0) +
                (requireDigit ? 1 : 0) +
                (requireSpecial ? 1 : 0);

            if (requiredGroupsCount == 0)
            {
                return Generate(length);
            }

            if (length < requiredGroupsCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    $"Length must be at least {requiredGroupsCount} when required groups are enabled.");
            }

            var allAlphabet = BuildAlphabet(
                includeUppercase: requireUppercase || true,
                includeLowercase: requireLowercase || true,
                includeDigits: requireDigit || true,
                includeSpecial: requireSpecial || true,
                specialAlphabet: specialAlphabet);

            var result = new char[length];
            var index = 0;

            if (requireUppercase)
                result[index++] = GetRandomChar(Uppercase);

            if (requireLowercase)
                result[index++] = GetRandomChar(Lowercase);

            if (requireDigit)
                result[index++] = GetRandomChar(Digits);

            if (requireSpecial)
                result[index++] = GetRandomChar(specialAlphabet);

            while (index < result.Length)
            {
                result[index++] = GetRandomChar(allAlphabet);
            }

            Shuffle(result);

            return new string(result);
        }

        /// <summary>
        /// Generates a URL-safe token with required character categories.
        /// </summary>
        public static string GenerateUrlSafeStrong(int length = 40)
        {
            return GenerateStrong(
                length: length,
                requireUppercase: true,
                requireLowercase: true,
                requireDigit: true,
                requireSpecial: !string.IsNullOrEmpty(UrlSafeSpecial),
                specialAlphabet: UrlSafeSpecial);
        }

        /// <summary>
        /// Generates a token only for URL-safe scenarios without group guarantees.
        /// </summary>
        public static string GenerateUrlSafe(int length = 40)
        {
            return Generate(length, DefaultUrlSafeAlphabet);
        }

        private static char GetRandomChar(string alphabet)
        {
            return alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        private static void Shuffle(Span<char> buffer)
        {
            for (var i = buffer.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
        }

        private static string BuildAlphabet(
            bool includeUppercase,
            bool includeLowercase,
            bool includeDigits,
            bool includeSpecial,
            string specialAlphabet)
        {
            var alphabet = string.Empty;

            if (includeUppercase)
                alphabet += Uppercase;

            if (includeLowercase)
                alphabet += Lowercase;

            if (includeDigits)
                alphabet += Digits;

            if (includeSpecial)
                alphabet += specialAlphabet;

            ValidateAlphabet(alphabet);

            return alphabet;
        }

        private static void ValidateLength(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 0.");
        }

        private static void ValidateAlphabet(string alphabet, string paramName = "alphabet")
        {
            if (string.IsNullOrWhiteSpace(alphabet))
                throw new ArgumentException("Alphabet cannot be null, empty, or whitespace.", paramName);

            if (alphabet.Length == 0)
                throw new ArgumentException("Alphabet cannot be empty.", paramName);

            Span<bool> seen = stackalloc bool[char.MaxValue + 1];

            foreach (var ch in alphabet)
            {
                if (seen[ch])
                {
                    throw new ArgumentException(
                        $"Alphabet contains duplicate character '{ch}'.",
                        paramName);
                }

                seen[ch] = true;
            }
        }
    }
}
