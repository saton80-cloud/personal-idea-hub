using System.Security.Cryptography;
using System.Text;

namespace PersonalWorkBoard.Server.Security;

public static class TokenUtility
{
    public static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
