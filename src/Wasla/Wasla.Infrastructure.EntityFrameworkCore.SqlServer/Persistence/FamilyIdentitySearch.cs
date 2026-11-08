using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Patients;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal static class FamilyIdentitySearch
{
    private static readonly (string From, string To)[] Replacements =
        Enumerable.Range(0x064B, 0x15).Select(c => (((char)c).ToString(), string.Empty))
            .Concat(new[] { ("ـ", ""), ("ٰ", ""), ("أ", "ا"), ("إ", "ا"), ("آ", "ا"), ("ٱ", "ا"), ("ى", "ي") }).ToArray();
    public static string Normalize(string name)
    {
        var value = name.Trim().ToUpperInvariant();
        foreach (var pair in Replacements) value = value.Replace(pair.From, pair.To, StringComparison.Ordinal);
        return value;
    }
    public static IQueryable<Patient> Exact(IQueryable<Patient> query, string name)
    {
        var patient = Expression.Parameter(typeof(Patient), "p");
        Expression value = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(string)], patient, Expression.Constant("NormalizedSearchNameAr"));
        var replace = typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;
        foreach (var pair in Replacements) value = Expression.Call(value, replace, Expression.Constant(pair.From), Expression.Constant(pair.To));
        return query.Where(Expression.Lambda<Func<Patient, bool>>(Expression.Equal(value, Expression.Constant(Normalize(name))), patient));
    }
}
