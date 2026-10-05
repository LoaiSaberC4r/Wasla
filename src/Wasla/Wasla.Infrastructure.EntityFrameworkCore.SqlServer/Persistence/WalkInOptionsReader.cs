using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Tickets.GetWalkInOptions;
using Wasla.Domain.Practices;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class WalkInOptionsReader(WaslaDbContext dbContext) : IWalkInOptionsReader
{
    public async Task<IReadOnlyList<WalkInSegmentOptionResponse>> ListAsync(
        Guid practiceId,
        DoctorPracticeVisitTypeCode visitType = DoctorPracticeVisitTypeCode.NewConsultation,
        CancellationToken cancellationToken = default)
    {
        var options = await (from price in dbContext.DoctorPracticeSegmentVisitTypePrices.AsNoTracking()
            join segment in dbContext.DoctorPracticeSegments.AsNoTracking()
                on price.SegmentId equals segment.Id
            join catalogVisitType in dbContext.DoctorPracticeVisitTypes.AsNoTracking()
                on price.VisitTypeId equals catalogVisitType.Id
            where price.DoctorPracticeId == practiceId &&
                  segment.DoctorPracticeId == practiceId && segment.IsActive &&
                  catalogVisitType.DoctorPracticeId == practiceId && catalogVisitType.IsActive &&
                  catalogVisitType.Type == visitType
            orderby segment.Priority descending, segment.NameAr, segment.Id, catalogVisitType.Id
            select new
            {
                SegmentId = segment.Id,
                segment.NameAr,
                segment.NameEn,
                segment.Priority,
                VisitTypeId = catalogVisitType.Id,
                VisitTypeNameAr = catalogVisitType.NameAr,
                VisitTypeNameEn = catalogVisitType.NameEn,
                price.Price
            }).ToListAsync(cancellationToken);

        return options.GroupBy(option => new
            {
                option.SegmentId, option.NameAr, option.NameEn, option.Priority
            })
            .Select(group => new WalkInSegmentOptionResponse(
                group.Key.SegmentId, group.Key.NameAr, group.Key.NameEn, group.Key.Priority,
                group.Select(option => new WalkInVisitTypeOptionResponse(
                    option.VisitTypeId, visitType.ToString(),
                    option.VisitTypeNameAr, option.VisitTypeNameEn, option.Price)).ToArray()))
            .ToArray();
    }
}
