using System.Security.Cryptography;
using System.Text;

namespace ArchiveManagement.Infrastructure;

/// <summary>密码哈希（PBKDF2-SHA256，内置实现，无额外依赖）</summary>
public static class SecurityHelper
{
    public static void HashPassword(string password, out string hash, out string salt)
    {
        salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        hash = HashWithSalt(password, salt);
    }

    public static string HashWithSalt(string password, string salt)
    {
        using var derive = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 100_000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(derive.GetBytes(32));
    }

    public static bool Verify(string password, string hash, string salt)
        => HashWithSalt(password, salt) == hash;
}
