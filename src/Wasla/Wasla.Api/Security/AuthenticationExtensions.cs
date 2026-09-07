using System.Text;
using BuildingBlock.Api.Security;
using BuildingBlock.Application.Abstraction.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Wasla.Infrastructure.Security;

namespace Wasla.Api.Security;

internal static class AuthenticationExtensions
{
    public static IServiceCollection AddWaslaAuthentication(
        this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtAccessor) =>
            {
                var jwt = jwtAccessor.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtClaimTypesCustom.UserName,
                    RoleClaimType = JwtClaimTypesCustom.Role
                };
            });
        services.AddAuthorization();
        services.AddBuildingBlockCurrentUser();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.Replace(ServiceDescriptor.Singleton<
            IAuthorizationMiddlewareResultHandler,
            WaslaAuthorizationResultHandler>());
        return services;
    }
}
