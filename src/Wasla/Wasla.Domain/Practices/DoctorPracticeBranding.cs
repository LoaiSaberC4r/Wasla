using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Practices;

public sealed class DoctorPracticeBranding : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPracticeBranding()
    {
    }

    private DoctorPracticeBranding(Guid id, Guid doctorPracticeId, Guid? actorId)
        : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        PrimaryColor = DoctorPracticePlatformDefaults.PrimaryColor;
        SecondaryColor = DoctorPracticePlatformDefaults.SecondaryColor;
        BackgroundColor = DoctorPracticePlatformDefaults.BackgroundColor;
        TextColor = DoctorPracticePlatformDefaults.TextColor;
        CreatedByApplicationUserId = actorId;
    }

    public Guid DoctorPracticeId { get; private set; }
    public string? LogoMediaKey { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string? BackgroundColor { get; private set; }
    public string? TextColor { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPracticeBranding> CreateDefault(
        Guid id,
        Guid doctorPracticeId,
        Guid? actorId = null)
        => id == Guid.Empty || doctorPracticeId == Guid.Empty
            ? Result<DoctorPracticeBranding>.Fail(DoctorPracticeBrandingErrors.Invalid)
            : Result<DoctorPracticeBranding>.Ok(new DoctorPracticeBranding(id, doctorPracticeId, actorId));

    public Result UpdateTheme(
        string? primaryColor,
        string? secondaryColor,
        string? backgroundColor,
        string? textColor,
        Guid? actorId = null)
    {
        var colors = new[] { primaryColor, secondaryColor, backgroundColor, textColor };
        if (colors.Any(color => !IsValidOptionalColor(color)))
        {
            return Result.Fail(DoctorPracticeBrandingErrors.InvalidColor);
        }

        PrimaryColor = NormalizeColor(primaryColor);
        SecondaryColor = NormalizeColor(secondaryColor);
        BackgroundColor = NormalizeColor(backgroundColor);
        TextColor = NormalizeColor(textColor);
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result ReplaceLogo(string logoMediaKey, Guid? actorId = null)
    {
        var normalized = logoMediaKey?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 1000)
        {
            return Result.Fail(DoctorPracticeBrandingErrors.InvalidLogo);
        }

        LogoMediaKey = normalized;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result RemoveLogo(bool practiceIsActive, Guid? actorId = null)
    {
        if (practiceIsActive)
        {
            return Result.Fail(DoctorPracticeBrandingErrors.ActivePracticeRequiresLogo);
        }

        LogoMediaKey = null;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    private static bool IsValidOptionalColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var color = value.Trim();
        return color.Length == 7 && color[0] == '#' && color[1..].All(Uri.IsHexDigit);
    }

    private static string? NormalizeColor(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}

public static class DoctorPracticeBrandingErrors
{
    public static Error Invalid => Error.Validation(
        "DoctorPracticeBranding.Invalid",
        DoctorPracticeErrors.Text("DoctorPracticeBrandingInvalid"));
    public static Error InvalidColor => Error.Validation(
        "DoctorPracticeBranding.InvalidColor",
        DoctorPracticeErrors.Text("DoctorPracticeBrandingInvalidColor"));
    public static Error InvalidLogo => Error.Validation(
        "DoctorPracticeBranding.InvalidLogo",
        DoctorPracticeErrors.Text("DoctorPracticeBrandingInvalidLogo"));
    public static Error ActivePracticeRequiresLogo => Error.Conflict(
        "DoctorPracticeBranding.ActivePracticeRequiresLogo",
        DoctorPracticeErrors.Text("DoctorPracticeBrandingActivePracticeRequiresLogo"));
    public static Error NotFound => Error.NotFound(
        "DoctorPracticeBranding.NotFound",
        DoctorPracticeErrors.Text("DoctorPracticeBrandingNotFound"));
    public static Error LogoNotFound => Error.NotFound(
        "DoctorPracticeBranding.LogoNotFound",
        DoctorPracticeErrors.Text("DoctorPracticeBrandingLogoNotFound"));
    public static Error ConcurrencyConflict => Error.Conflict(
        "DoctorPracticeBranding.ConcurrencyConflict",
        DoctorPracticeErrors.Text("DoctorPracticeBrandingConcurrencyConflict"));
}
