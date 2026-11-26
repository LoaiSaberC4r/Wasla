namespace Wasla.Infrastructure.Email;

public sealed class EmailBrandingOptions
{
    public const string SectionName = "EmailBranding";

    public string ApplicationNameAr { get; set; } = "وصلة";
    public string ApplicationNameEn { get; set; } = "Wasla";
    public string TaglineAr { get; set; } = "رعاية صحية أكثر ترابطًا";
    public string TaglineEn { get; set; } = "More Connected Healthcare";

    public string[] AllowedOrigins { get; set; } = [];
    public string? AssetBaseUrl { get; set; }
    public string? TimeZoneId { get; set; }
    public string? LogoUrl { get; set; }
    public string? FooterImageUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? SupportUrl { get; set; }
    public string? PrivacyPolicyUrl { get; set; }
    public string? LoginUrl { get; set; }
    public string? PatientActivationUrl { get; set; }
    public string? PatientActivationTokenQueryParameter { get; set; }
    public string? PatientIdentityClaimUrl { get; set; }
    public string? DoctorSpecializationRequestUrl { get; set; }
    public string? FamilyUrl { get; set; }
    public string? FamilyRequestUrl { get; set; }
    public string? ReservationUrl { get; set; }
    public string? SubscriptionUrl { get; set; }
    public string? SubscriptionPlansUrl { get; set; }

    public string PrimaryColor { get; set; } = "#087F75";
    public string PrimaryLightColor { get; set; } = "#E8F5F2";
    public string HeadingColor { get; set; } = "#172B36";
    public string BodyColor { get; set; } = "#334155";
    public string MutedColor { get; set; } = "#64748B";
    public string BackgroundColor { get; set; } = "#EDF2F4";
    public string SuccessColor { get; set; } = "#166534";
    public string WarningColor { get; set; } = "#B45309";
    public string DangerColor { get; set; } = "#B91C1C";
}
