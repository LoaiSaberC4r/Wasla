using BuildingBlock.Domain.Results;
using Wasla.Domain.Families;

namespace Wasla.Application.Features.Families;

public interface IFamilyRelationshipMedicalVerification
{
    bool AcceptsEvidence(IReadOnlyList<FamilyEvidenceUpload> evidence);
    Task<Result> RecordAsync(FamilyRelationshipRequest request, Family family, CancellationToken ct);
}
