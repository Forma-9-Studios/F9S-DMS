using System.Security.Cryptography;

namespace F9SDMS.Services
{
    /// <summary>Password rules shown to people, and temporary passwords for admins to hand out.</summary>
    public static class PasswordHelper
    {
        public const int MinLength = 8;

        private static readonly string[] Words =
        {
            "Plum", "Cedar", "River", "Maple", "Coral", "Amber", "Delta", "Falcon", "Harbor", "Lotus",
            "Mango", "Orbit", "Pixel", "Quartz", "Sierra", "Tiger", "Violet", "Willow", "Summit", "Atlas"
        };

        /// <summary>
        /// A temporary password that meets the rules and is easy to read out: e.g. "Cedar-4827-kz".
        /// </summary>
        public static string GenerateTemporary()
        {
            var word = Words[RandomNumberGenerator.GetInt32(Words.Length)];
            var digits = RandomNumberGenerator.GetInt32(1000, 10000);
            const string letters = "abcdefghjkmnpqrstuvwxyz";
            var tail = new string(new[] { letters[RandomNumberGenerator.GetInt32(letters.Length)], letters[RandomNumberGenerator.GetInt32(letters.Length)] });
            return $"{word}-{digits}-{tail}";
        }

        /// <summary>The rules, in the order they're shown, with whether <paramref name="password"/> meets each.</summary>
        public static IReadOnlyList<(string Text, bool Met)> Check(string? password)
        {
            password ??= "";
            return new[]
            {
                ($"At least {MinLength} characters", password.Length >= MinLength),
                ("An uppercase and a lowercase letter", password.Any(char.IsUpper) && password.Any(char.IsLower)),
                ("A number", password.Any(char.IsDigit))
            };
        }
    }
}
