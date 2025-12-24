using System.Security.Cryptography;

namespace DocControl.Core.Security;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static (string hash, string salt, string keySalt) HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var keySaltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Hash(password, saltBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes), Convert.ToBase64String(keySaltBytes));
    }

    public static bool Verify(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
        {
            return false;
        }

        byte[] saltBytes;
        byte[] hashBytes;
        try
        {
            saltBytes = Convert.FromBase64String(storedSalt);
            hashBytes = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var computed = Hash(password, saltBytes);
        return CryptographicOperations.FixedTimeEquals(hashBytes, computed);
    }

    public static byte[] DeriveEncryptionKey(string storedHash, string keySalt)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(keySalt))
        {
            throw new ArgumentException("Stored hash and key salt are required.");
        }

        var hashBytes = Convert.FromBase64String(storedHash);
        var keySaltBytes = Convert.FromBase64String(keySalt);
        using var derive = new Rfc2898DeriveBytes(hashBytes, keySaltBytes, Iterations, HashAlgorithmName.SHA256);
        return derive.GetBytes(KeySize);
    }

    private static byte[] Hash(string password, byte[] saltBytes)
    {
        using var derive = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
        return derive.GetBytes(KeySize);
    }
}
