using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using BuildingBlock.Domain.Results;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Domain.Operations;

public static partial class OperationalNotePolicy
{
    public static Result<string> Sanitize(string? note, int maximumLength = 2000)
    {
        var normalized = note?.Trim().Normalize(NormalizationForm.FormKC) ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength || SensitivePattern().IsMatch(normalized) ||
            normalized.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) ||
            IdentifierPattern().IsMatch(normalized) || normalized.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("https://", StringComparison.OrdinalIgnoreCase) || normalized.Contains("file://", StringComparison.OrdinalIgnoreCase))
            return Result<string>.Fail(OperatingFinanceErrors.Invalid("Operations.NoteContainsRestrictedData"));
        return Result<string>.Ok(WebUtility.HtmlEncode(normalized));
    }
    [GeneratedRegex(@"diagnos|medical\s+history|clinical\s+notes|prescription|lab\s+result|radiology\s+result|national\s*id|phone\s*:|salary|payroll|diabetes|cancer|hypertension|تشخيص|تاريخ\s*مرضي|روشتة|راتب|مرتبات|نتيجة\s*تحليل|رقم\s*قومي|سكري|سرطان|ضغط\s*الدم|(?:EGP|جنيه)\s*\d|\d\s*(?:EGP|جنيه)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitivePattern();
    [GeneratedRegex(@"(?:\p{Nd}[\s\-()]*){7,}", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}
