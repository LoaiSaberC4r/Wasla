using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Wasla.Application.Security;

namespace Wasla.Infrastructure.Security;

internal sealed class PasswordResetProtector(IOptions<PasswordResetOptions> options)
    : IPasswordResetProtector
{
    private readonly byte[] _secret = Encoding.UTF8.GetBytes(options.Value.HmacSecret);

    public string GenerateOtp(int length)
    {
        if (length is < 4 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var upperBound = (int)Math.Pow(10, length);
        return RandomNumberGenerator.GetInt32(0, upperBound).ToString($"D{length}", System.Globalization.CultureInfo.InvariantCulture);
    }

    public string GenerateResetToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string HashOtp(string otp) => Hash("otp:", otp);

    public string HashResetToken(string resetToken) => Hash("reset:", resetToken);

    public bool VerifyOtp(string otp, string hash) => Verify("otp:", otp, hash);

    public bool VerifyResetToken(string resetToken, string hash) => Verify("reset:", resetToken, hash);

    private string Hash(string purpose, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        using var hmac = new HMACSHA256(_secret);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(purpose + value)));
    }

    private bool Verify(string purpose, string value, string hash)
    {
        try
        {
            var supplied = Convert.FromHexString(Hash(purpose, value));
            var expected = Convert.FromHexString(hash);
            return supplied.Length == expected.Length &&
                   CryptographicOperations.FixedTimeEquals(supplied, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

