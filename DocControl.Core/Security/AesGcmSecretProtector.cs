using System.Security.Cryptography;
using System.Text;

namespace DocControl.Core.Security;

public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] key;

    public AesGcmSecretProtector(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new ArgumentException("Encryption key is required.", nameof(base64Key));
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(base64Key);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Encryption key must be base64.", nameof(base64Key), ex);
        }

        if (decoded.Length != 32)
        {
            throw new ArgumentException("Encryption key must be 32 bytes when decoded from base64.", nameof(base64Key));
        }

        key = decoded;
    }

    public AesGcmSecretProtector(byte[] keyBytes)
    {
        if (keyBytes is null || keyBytes.Length != 32)
        {
            throw new ArgumentException("Encryption key must be 32 bytes.", nameof(keyBytes));
        }
        key = keyBytes;
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        var payload = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length + tag.Length, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string? Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText)) return null;
        if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal)) return null;

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(cipherText[Prefix.Length..]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (payload.Length < NonceSize + TagSize)
        {
            return null;
        }

        var nonce = payload.AsSpan(0, NonceSize).ToArray();
        var tag = payload.AsSpan(NonceSize, TagSize).ToArray();
        var cipherBytes = payload.AsSpan(NonceSize + TagSize).ToArray();
        var plainBytes = new byte[cipherBytes.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }

        return Encoding.UTF8.GetString(plainBytes);
    }
}
