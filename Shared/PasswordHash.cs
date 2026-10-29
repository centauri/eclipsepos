using System;
using System.Security.Cryptography;
using System.Text;

namespace EclipsePOS.Platform
{
    public static class PasswordHash
    {
        public static string Create(string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("A password is required.");
            var salt = new byte[16];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            using (var derive = new Rfc2898DeriveBytes(password, salt, 600000, HashAlgorithmName.SHA256))
                return "pbkdf2-sha256$600000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(derive.GetBytes(32));
        }

        public static bool Verify(string password, string stored)
        {
            if (password == null || string.IsNullOrEmpty(stored)) return false;
            if (stored.Length == 32)
            {
                // Read-only compatibility. Reset old passwords to replace these hashes.
                using (var md5 = MD5.Create())
                    return string.Equals(BitConverter.ToString(md5.ComputeHash(Encoding.Default.GetBytes(password))).Replace("-", ""), stored, StringComparison.OrdinalIgnoreCase);
            }
            try
            {
                var parts = stored.Split('$');
                int iterations;
                if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out iterations) || iterations < 100000 || iterations > 2000000) return false;
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                if (salt.Length != 16 || expected.Length != 32) return false;
                using (var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                {
                    var actual = derive.GetBytes(32);
                    int difference = 0;
                    for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
                    return difference == 0;
                }
            }
            catch (FormatException) { return false; }
        }
    }
}
