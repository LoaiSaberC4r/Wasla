namespace BuildingBlock.Application.Abstraction.Encryption
{
    public interface IEncryptionService
    {
        /// <summary>Encrypts plain text and returns a transport-safe Base64 payload.</summary>
        string Encrypt(string plaintext);

        /// <summary>Decrypts an encrypted Base64 payload and returns the plain text.</summary>
        string Decrypt(string ciphertextBase64);

        /// <summary>Decrypts an encrypted payload and reports whether it should be encrypted again with the current key format.</summary>
        EncryptionDecryptionResult DecryptDetailed(string ciphertextBase64);
    }
}
