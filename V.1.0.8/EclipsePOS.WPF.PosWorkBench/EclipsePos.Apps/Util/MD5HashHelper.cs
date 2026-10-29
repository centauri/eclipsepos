namespace EclipsePos.Apps.Util
{
    public class MD5HashHelper
    {
        // Historical API name retained for existing callers; new writes use PBKDF2.
        public static string getMd5Hash(string input) { return EclipsePOS.Platform.PasswordHash.Create(input); }
        public static bool verifyMd5Hash(string input, string hash) { return EclipsePOS.Platform.PasswordHash.Verify(input, hash); }
    }
}
