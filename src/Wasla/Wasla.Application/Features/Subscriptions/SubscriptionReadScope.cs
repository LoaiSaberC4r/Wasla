using System.Linq.Expressions;

namespace Wasla.Application.Features.Subscriptions;

public sealed record SubscriptionReadWindow(string FeatureCode, DateTime? StartsAtUtc, DateTime EndsAtUtc);
public sealed record SubscriptionReadScope(bool CurrentEntitlement, IReadOnlyList<SubscriptionReadWindow> Windows)
{
    public bool HasAccess => CurrentEntitlement || Windows.Count != 0;
    public bool Covers(DateTime createdAtUtc) => CurrentEntitlement || Windows.Any(w =>
        (w.StartsAtUtc == null || createdAtUtc >= w.StartsAtUtc) && createdAtUtc < w.EndsAtUtc);

    // Compose a provider-translatable predicate before counts, cursor selection and pagination.
    public Expression<Func<T, bool>> Predicate<T>(Expression<Func<T, DateTime>> createdAt)
    {
        Expression body = Expression.Constant(CurrentEntitlement);
        if (!CurrentEntitlement)
            foreach (var window in Windows)
            {
                Expression within = Expression.LessThan(createdAt.Body, Expression.Constant(window.EndsAtUtc));
                if (window.StartsAtUtc is { } start)
                    within = Expression.AndAlso(Expression.GreaterThanOrEqual(createdAt.Body, Expression.Constant(start)), within);
                body = Expression.OrElse(body, within);
            }
        return Expression.Lambda<Func<T, bool>>(body, createdAt.Parameters);
    }
}
public sealed record SubscriptionPermissionRule(string Permission, string FeatureCode, Wasla.Domain.Subscriptions.FeatureAccessLevel RequiredAccess);
public sealed record SubscriptionPermissionDecision(bool Allowed, string? BlockedReason = null, SubscriptionReadScope? ReadScope = null);
