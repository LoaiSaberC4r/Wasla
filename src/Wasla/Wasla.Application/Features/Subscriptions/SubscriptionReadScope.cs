using System.Linq.Expressions;

namespace Wasla.Application.Features.Subscriptions;

public sealed record SubscriptionReadWindow(string FeatureCode, DateTime? StartsAtUtc, DateTime EndsAtUtc, Guid? PracticeId = null, Guid? SubscriptionId = null, bool Current = false, Wasla.Domain.Subscriptions.FeatureAccessLevel AccessLevel = Wasla.Domain.Subscriptions.FeatureAccessLevel.ReadOnly);
public sealed record SubscriptionReadScope(bool CurrentEntitlement, IReadOnlyList<SubscriptionReadWindow> Windows, IReadOnlyList<Guid>? CurrentPracticeIds = null)
{
    public bool HasAccess => CurrentEntitlement || Windows.Count != 0 || CurrentPracticeIds?.Count > 0;
    public bool Covers(DateTime createdAtUtc, Guid? practiceId = null) => CurrentEntitlement ||
        practiceId.HasValue && CurrentPracticeIds?.Contains(practiceId.Value) == true || Windows.Any(w =>
        (w.PracticeId is null || practiceId == w.PracticeId) && (w.StartsAtUtc == null || createdAtUtc >= w.StartsAtUtc) && createdAtUtc < w.EndsAtUtc);

    // Compose a provider-translatable predicate before counts, cursor selection and pagination.
    public Expression<Func<T, bool>> Predicate<T>(Expression<Func<T, DateTime>> createdAt, Expression<Func<T, Guid?>>? practice = null)
    {
        Expression body = Expression.Constant(CurrentEntitlement);
        if (practice is not null && CurrentPracticeIds is { Count: > 0 })
        {
            var practiceBody = new ParameterReplacement(practice.Parameters[0], createdAt.Parameters[0]).Visit(practice.Body);
            foreach (var id in CurrentPracticeIds)
                body = Expression.OrElse(body, Expression.Equal(practiceBody, Expression.Constant((Guid?)id, typeof(Guid?))));
        }
        if (!CurrentEntitlement)
            foreach (var window in Windows)
            {
                Expression within = Expression.LessThan(createdAt.Body, Expression.Constant(window.EndsAtUtc));
                if (window.StartsAtUtc is { } start)
                    within = Expression.AndAlso(Expression.GreaterThanOrEqual(createdAt.Body, Expression.Constant(start)), within);
                if (window.PracticeId is { } practiceId)
                {
                    if (practice is null) continue;
                    var practiceBody = new ParameterReplacement(practice.Parameters[0], createdAt.Parameters[0]).Visit(practice.Body);
                    within = Expression.AndAlso(Expression.Equal(practiceBody, Expression.Constant((Guid?)practiceId, typeof(Guid?))), within);
                }
                body = Expression.OrElse(body, within);
            }
        return Expression.Lambda<Func<T, bool>>(body, createdAt.Parameters);
    }
    private sealed class ParameterReplacement(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : node;
    }
}
public sealed record SubscriptionPermissionRule(string Permission, string FeatureCode, Wasla.Domain.Subscriptions.FeatureAccessLevel RequiredAccess);
public sealed record SubscriptionPermissionDecision(bool Allowed, string? BlockedReason = null, SubscriptionReadScope? ReadScope = null);
