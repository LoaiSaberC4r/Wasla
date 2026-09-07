using System.Globalization;
using FluentValidation.Resources;
using Wasla.Domain.Resources;

namespace Wasla.Application.Common.Validation;

internal sealed class ErrorMessageLanguageManager : LanguageManager
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar");

    public ErrorMessageLanguageManager()
    {
        Add("NotNullValidator", nameof(ErrorMessage.ValidationNotNull));
        Add("NotEmptyValidator", nameof(ErrorMessage.ValidationNotEmpty));
        Add("EmailValidator", nameof(ErrorMessage.ValidationEmail));
        Add("MaximumLengthValidator", nameof(ErrorMessage.ValidationMaximumLength));
        Add("MinimumLengthValidator", nameof(ErrorMessage.ValidationMinimumLength));
        Add("ExactLengthValidator", nameof(ErrorMessage.ValidationExactLength));
        Add("LengthValidator", nameof(ErrorMessage.ValidationLength));
        Add("GreaterThanValidator", nameof(ErrorMessage.ValidationGreaterThan));
        Add("GreaterThanOrEqualValidator", nameof(ErrorMessage.ValidationGreaterThanOrEqual));
        Add("InclusiveBetweenValidator", nameof(ErrorMessage.ValidationInclusiveBetween));
        Add("EqualValidator", nameof(ErrorMessage.ValidationEqual));
        Add("RegularExpressionValidator", nameof(ErrorMessage.ValidationRegularExpression));
        Add("PredicateValidator", nameof(ErrorMessage.ValidationPredicate));
        Add("AsyncPredicateValidator", nameof(ErrorMessage.ValidationPredicate));
        Add("EnumValidator", nameof(ErrorMessage.ValidationEnum));
        Add("NullValidator", nameof(ErrorMessage.ValidationNull));
        Add("EmptyValidator", nameof(ErrorMessage.ValidationEmpty));
    }

    private void Add(string validatorName, string resourceName)
    {
        AddTranslation("en", validatorName, ErrorMessage.GetString(resourceName, English));
        AddTranslation("ar", validatorName, ErrorMessage.GetString(resourceName, Arabic));
    }
}

