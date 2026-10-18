using BuildingBlock.Domain.Results;
using MediatR;
namespace Wasla.Application.Features.PatientOnboarding;
internal sealed class PatientIdentityMediaBehavior<TRequest, TValue>(IPatientIdentityMediaCompensation compensation) : IPipelineBehavior<TRequest, Result<TValue>> where TRequest : notnull
{
    public async Task<Result<TValue>> Handle(TRequest request, RequestHandlerDelegate<Result<TValue>> next, CancellationToken ct)
    {
        var committed = false;
        try { var response = await next(ct); committed = response.IsSuccess; return response; }
        finally { if (committed) compensation.Clear(); else await compensation.CleanAsync(); }
    }
}
