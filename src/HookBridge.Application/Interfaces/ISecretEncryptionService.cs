namespace HookBridge.Application.Interfaces;

public interface ISecretEncryptionService
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);

    bool IsEncrypted(string value);
}
