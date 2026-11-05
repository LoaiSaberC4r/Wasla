using BuildingBlock.Domain.Results;
using BuildingBlock.Application.Abstraction.Security;
using MediatR;

namespace Wasla.Application.Features.Subscriptions;

public interface IPracticeCommercialRequest { Guid PracticeId { get; } }
[AttributeUsage(AttributeTargets.Class)]
public sealed class PracticeCommercialPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
// Runs inside the existing transaction behavior, so direct CQRS dispatch also rechecks
// stored permissions and scoped coverage under the same commercial lock as transfers.
internal sealed class PracticeCommercialAuthorizationBehavior<TRequest, TResponse>(ISubscriptionPermissionGuard guard, ICurrentUser current)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private static readonly string? Permission = (Attribute.GetCustomAttribute(typeof(TRequest), typeof(PracticeCommercialPermissionAttribute)) as PracticeCommercialPermissionAttribute)?.Permission;
    private static readonly Func<Error, TResponse> Failure = CreateFailure();
    private static Func<Error, TResponse> CreateFailure()
    {
        var method = typeof(TResponse).GetMethod(nameof(Result.Fail), [typeof(Error)])!;
        var error = System.Linq.Expressions.Expression.Parameter(typeof(Error));
        return System.Linq.Expressions.Expression.Lambda<Func<Error, TResponse>>(System.Linq.Expressions.Expression.Call(method, error), error).Compile();
    }
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IPracticeCommercialRequest practice) return await next(ct);
        if (Permission is null || current.UserId is not { } actor ||
            !await guard.AllowedForUserAsync(actor, Permission, ct, new(SubscriptionResourceKind.Practice, practice.PracticeId)))
            return Failure(Wasla.Domain.Subscriptions.SubscriptionErrors.Denied());
        return await next(ct);
    }
}
