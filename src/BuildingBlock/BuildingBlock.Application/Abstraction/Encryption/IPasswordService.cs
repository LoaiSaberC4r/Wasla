namespace BuildingBlock.Application.Abstraction.Encryption
{
    public sealed record PasswordVerification(bool IsValid, bool SuccessRehashNeeded);

    public interface IPasswordService
    {
        string Hash(string password);

        bool Verify(string password, string passwordHash);

        PasswordVerification VerifyDetailed(string password, string passwordHash);

        Task<string> HashAsync(string password, CancellationToken ct = default);

        Task<bool> VerifyAsync(string password, string passwordHash, CancellationToken ct = default);

        bool IsStrongPassword(string password);
    }
}
