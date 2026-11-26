using Microsoft.Extensions.Options;

namespace Wasla.Infrastructure.Email;

internal sealed class EmailBrandingProvider(IOptions<EmailBrandingOptions> options)
{
    internal EmailBrandingOptions Options { get; } = options.Value;
    internal EmailUrlValidator Urls { get; } = new(options.Value.AllowedOrigins);

    internal string? Destination(string key) => Urls.Validate(key switch
    {
        nameof(Options.SupportUrl) => Options.SupportUrl,
        nameof(Options.LoginUrl) => Options.LoginUrl,
        nameof(Options.PatientIdentityClaimUrl) => Options.PatientIdentityClaimUrl,
        nameof(Options.DoctorSpecializationRequestUrl) => Options.DoctorSpecializationRequestUrl,
        nameof(Options.FamilyUrl) => Options.FamilyUrl,
        nameof(Options.FamilyRequestUrl) => Options.FamilyRequestUrl,
        nameof(Options.ReservationUrl) => Options.ReservationUrl,
        nameof(Options.SubscriptionUrl) => Options.SubscriptionUrl,
        nameof(Options.SubscriptionPlansUrl) => Options.SubscriptionPlansUrl,
        _ => null
    });
}

