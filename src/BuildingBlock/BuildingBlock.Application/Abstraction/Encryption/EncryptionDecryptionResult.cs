namespace BuildingBlock.Application.Abstraction.Encryption
{
    public sealed record EncryptionDecryptionResult(
        string Plaintext,
        string? KeyId,
        bool ReEncryptionRequired);
}
