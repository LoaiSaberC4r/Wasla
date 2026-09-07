using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Wasla.Application.Security;

namespace Wasla.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "Wasla";
    public string Audience { get; set; } = "Wasla.Api";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}

internal sealed class JwtAccessTokenService(
    IOptions<JwtOptions> options,
    IDateTimeProvider clock)
    : IAccessTokenService
{
    public const string UserTypeClaim = "userType";
    public const string PasswordChangeRequiredClaim = "passwordChangeRequired";
    public const string DoctorIdClaim = "doctorId";
    public const string PatientIdClaim = "patientId";

    public GeneratedAccessToken Create(AccessTokenDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var now = clock.UtcNow;
        var expires = now.AddMinutes(options.Value.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, descriptor.ApplicationUserId.ToString()),
            new(JwtClaimTypesCustom.UserId, descriptor.ApplicationUserId.ToString()),
            new(JwtClaimTypesCustom.UserName, descriptor.UserName),
            new(JwtClaimTypesCustom.Email, descriptor.Email),
            new(UserTypeClaim, descriptor.UserType.ToString()),
            new(PasswordChangeRequiredClaim, descriptor.PasswordChangeRequired.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(descriptor.Roles.Select(role => new Claim(JwtClaimTypesCustom.Role, role)));
        claims.AddRange(descriptor.Permissions.Select(permission => new Claim(JwtClaimTypesCustom.Permission, permission)));
        if (descriptor.DoctorId is { } doctorId)
        {
            claims.Add(new Claim(DoctorIdClaim, doctorId.ToString()));
        }

        if (descriptor.PatientId is { } patientId)
        {
            claims.Add(new Claim(PatientIdClaim, patientId.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Value.Issuer,
            audience: options.Value.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);
        return new GeneratedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

