using System.Security.Cryptography;

namespace PersonalWorkBoard.Server.Security;

public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int HashSize = 32;

    public static (byte[] Salt, byte[] Hash) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(24);
        return (salt, Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashSize));
    }

    public static bool Verify(string password, byte[] salt, byte[] expected)
    {
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
