using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Tickets.GetWalkInOptions;
using Wasla.Domain.Practices;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class WalkInOptionsReader(WaslaDbContext dbContext) : IWalkInOptionsReader
{
    public async Task<IReadOnlyList<WalkInSegmentOptionResponse>> ListAsync(
        Guid practiceId,
        CancellationToken cancellationToken = default)
    {
        var options = await (from price in dbContext.DoctorPracticeSegmentVisitTypePrices.AsNoTracking()
            join segment in dbContext.DoctorPracticeSegments.AsNoTracking()
                on price.SegmentId equals segment.Id
            join visitType in dbContext.DoctorPracticeVisitTypes.AsNoTracking()
                on price.VisitTypeId equals visitType.Id
            where price.DoctorPracticeId == practiceId &&
                  segment.DoctorPracticeId == practiceId && segment.IsActive &&
                  visitType.DoctorPracticeId == practiceId && visitType.IsActive &&
                  visitType.Type == DoctorPracticeVisitTypeCode.NewConsultation
            orderby segment.Priority descending, segment.NameAr, segment.Id, visitType.Id
            select new
            {
                SegmentId = segment.Id,
                segment.NameAr,
                segment.NameEn,
                segment.Priority,
                VisitTypeId = visitType.Id,
                VisitTypeNameAr = visitType.NameAr,
                VisitTypeNameEn = visitType.NameEn,
                price.Price
            }).ToListAsync(cancellationToken);

        return options.GroupBy(option => new
            {
                option.SegmentId, option.NameAr, option.NameEn, option.Priority
            })
            .Select(group => new WalkInSegmentOptionResponse(
                group.Key.SegmentId, group.Key.NameAr, group.Key.NameEn, group.Key.Priority,
                group.Select(option => new WalkInVisitTypeOptionResponse(
                    option.VisitTypeId, DoctorPracticeVisitTypeCode.NewConsultation.ToString(),
                    option.VisitTypeNameAr, option.VisitTypeNameEn, option.Price)).ToArray()))
            .ToArray();
    }
}
