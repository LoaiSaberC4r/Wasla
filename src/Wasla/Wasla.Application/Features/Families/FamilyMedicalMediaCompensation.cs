using BuildingBlock.Domain.Results;
using MediatR;

namespace Wasla.Application.Features.Families;

public interface IFamilyMedicalMediaCompensation
{
    void Track(string key);
    void Clear();
    Task CleanAsync();
}
internal sealed class FamilyMedicalMediaCompensationBehavior(IFamilyMedicalMediaCompensation compensation)
    : IPipelineBehavior<FamilyMedicalCommand, Result<object>>
{
    public async Task<Result<object>> Handle(FamilyMedicalCommand request, RequestHandlerDelegate<Result<object>> next, CancellationToken ct)
    {
        var committed = false;
        try { var result = await next(ct); committed = result.IsSuccess; return result; }
        finally { if (committed) compensation.Clear(); else await compensation.CleanAsync(); }
    }
}
