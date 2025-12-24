namespace DocControl.Core.Security;

public interface ISecretProtector
{
    string Encrypt(string plaintext);
    string? Decrypt(string cipherText);
}
