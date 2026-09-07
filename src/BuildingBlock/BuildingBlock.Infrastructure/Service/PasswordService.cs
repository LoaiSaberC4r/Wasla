using BuildingBlock.Application.Abstraction.Encryption;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace BuildingBlock.Infrastructure.Service
{
    internal sealed class PasswordService : IPasswordService
    {
        private readonly PasswordHasher<object> _hasher = new();
        private readonly PasswordPolicyOptions _policy;

        public PasswordService()
            : this(Microsoft.Extensions.Options.Options.Create(new PasswordPolicyOptions()))
        {
        }

        public PasswordService(IOptions<PasswordPolicyOptions> policy)
        {
            _policy = policy.Value;
        }

        public string Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be empty.", nameof(password));
            }

            if (password.Length > _policy.MaximumLength)
            {
                throw new ArgumentException("Password exceeds the configured maximum length.", nameof(password));
            }

            return _hasher.HashPassword(user: null!, password);
        }

        public bool Verify(string password, string passwordHash)
            => VerifyDetailed(password, passwordHash).IsValid;

        public PasswordVerification VerifyDetailed(string password, string passwordHash)
        {
            if (string.IsNullOrEmpty(password) ||
                password.Length > _policy.MaximumLength ||
                string.IsNullOrWhiteSpace(passwordHash))
            {
                return new PasswordVerification(false, false);
            }

            PasswordVerificationResult result;
            try
            {
                result = _hasher.VerifyHashedPassword(
                    user: null!,
                    hashedPassword: passwordHash,
                    providedPassword: password);
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                return new PasswordVerification(false, false);
            }

            return result switch
            {
                PasswordVerificationResult.Success => new PasswordVerification(true, false),
                PasswordVerificationResult.SuccessRehashNeeded => new PasswordVerification(true, true),
                _ => new PasswordVerification(false, false)
            };
        }

        public Task<string> HashAsync(string password, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Hash(password));
        }

        public Task<bool> VerifyAsync(string password, string passwordHash, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Verify(password, passwordHash));
        }

        public bool IsStrongPassword(string password)
        {
            if (string.IsNullOrEmpty(password) ||
                password.Length < _policy.RequiredLength ||
                password.Length > _policy.MaximumLength)
            {
                return false;
            }

            return (!_policy.RequireDigit || password.Any(char.IsDigit)) &&
                   (!_policy.RequireUppercase || password.Any(char.IsUpper)) &&
                   (!_policy.RequireLowercase || password.Any(char.IsLower)) &&
                   (!_policy.RequireNonAlphanumeric || password.Any(character => !char.IsLetterOrDigit(character)));
        }
    }
}
