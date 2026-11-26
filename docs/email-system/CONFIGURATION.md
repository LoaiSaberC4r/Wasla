# Email branding and deployment

The API binds the `EmailBranding` section. Missing optional values fall back to text branding or omit the relevant link. Every new destination uses HTTPS with an exact origin listed in `AllowedOrigins`. Configure origins as scheme, host, and optional port. For environment variables, use keys such as `EmailBranding__AllowedOrigins__0` and `EmailBranding__LoginUrl`.

| Setting | Purpose |
|---|---|
| `ApplicationNameAr`, `ApplicationNameEn`, `TaglineAr`, `TaglineEn` | Default to the Wasla brand and approved taglines |
| `LogoUrl` | Optional official logo on an allowed origin |
| `FooterImageUrl` | Optional legacy footer image. An explicit HTTPS image remains compatible when no destination origins are configured |
| `WebsiteUrl`, `SupportUrl`, `PrivacyPolicyUrl` | Optional shared footer destinations. Support also serves doctor rejection and suspension actions |
| `LoginUrl` | Doctor approval and reactivation, reception account creation, and generic clinical sign-in |
| `PatientActivationUrl`, `PatientActivationTokenQueryParameter` | Patient activation page and verified query parameter contract |
| `PatientIdentityClaimUrl`, `DoctorSpecializationRequestUrl` | Existing authenticated or verification-gated follow-up pages |
| `FamilyUrl`, `FamilyRequestUrl` | Family account and request management |
| `ReservationUrl` | Existing authenticated reservation page. The renderer passes no reservation identifiers in the link |
| `SubscriptionUrl`, `SubscriptionPlansUrl` | Existing account subscription and plan pages |
| `PrimaryColor`, `PrimaryLightColor`, `HeadingColor`, `BodyColor`, `MutedColor`, `BackgroundColor`, `SuccessColor`, `WarningColor`, `DangerColor` | Optional six-digit hex colors. Invalid values fall back to the approved palette |

## Frontend destinations

The repository provides no verified production Frontend destinations for these emails. Links stay omitted until the relevant page and authorized origin are configured. For activation, also verify that the page accepts the configured token query parameter and consumes the invitation only when the patient submits the existing activation operation successfully. Keep the configured activation route free of query strings and fragments. Without that configuration, the email displays the raw code using the existing token workflow.

The API base configuration sets the existing SMTP sender display name to `Wasla | وصلة`. Use the deployed authorized sender address and existing SMTP settings. The former placeholder Footer URL is empty in source configuration. Supply approved branding URLs through deployment configuration where available.
