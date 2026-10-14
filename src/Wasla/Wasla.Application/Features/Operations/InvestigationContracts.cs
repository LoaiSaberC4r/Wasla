using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Persistence;
using Wasla.Domain.Operations;

namespace Wasla.Application.Features.Operations;

public sealed record InvestigationCreateRequest(string TitleAr, string? TitleEn, string Description, InvestigationIssueType IssueType,
    InvestigationPriority Priority = InvestigationPriority.Normal, InvestigationResourceType LinkedResourceType = InvestigationResourceType.None, Guid? LinkedResourceId = null);
public sealed record InvestigationActionRequest(string RowVersion, Guid? AssignedSuperAdminId = null,
    InvestigationResolutionType? ResolutionType = null, string? Notes = null);
public sealed record InvestigationFilter(InvestigationStatus? Status = null, InvestigationIssueType? IssueType = null,
    InvestigationPriority? Priority = null, Guid? AssignedSuperAdminId = null, DateOnly? FromDate = null,
    DateOnly? ToDate = null, int PageNumber = 1, int PageSize = 20, string SortDirection = "desc");
public sealed record InvestigationResponse(Guid Id, string TitleAr, string? TitleEn, string Description, InvestigationIssueType IssueType,
    InvestigationPriority Priority, InvestigationResourceType LinkedResourceType, Guid? LinkedResourceId, Guid CreatedByApplicationUserId,
    Guid? AssignedSuperAdminId, InvestigationStatus Status, InvestigationResolutionType? ResolutionType, string? ResolutionNotes,
    string? ClosureNotes, DateTime CreatedOnUtc, DateTime ModifiedOnUtc, DateTime? ResolvedOnUtc, DateTime? ClosedOnUtc,
    string RowVersion, IReadOnlyList<ReportAction> AvailableActions);
public sealed record InvestigationHistoryResponse(Guid Id, string Operation, InvestigationStatus Status, Guid ActorApplicationUserId,
    Guid? AssignedSuperAdminId, InvestigationResolutionType? ResolutionType, string? Note, DateTime RecordedAtUtc, int SequenceNumber);
public sealed record InvestigationFilterOptions(IReadOnlyList<ReportOption> Statuses, IReadOnlyList<ReportOption> IssueTypes,
    IReadOnlyList<ReportOption> Priorities, IReadOnlyList<ReportOption> ResolutionTypes, IReadOnlyList<ReportOption> LinkedResourceTypes,
    IReadOnlyList<ReportOption> ActiveSuperAdmins, long TotalActiveSuperAdmins = 0, int PageNumber = 1, int PageSize = 50);
public enum InvestigationOperation { Create = 1, Assign = 2, StartReview = 3, Resolve = 4, Close = 5, Reopen = 6 }
public sealed record WriteInvestigationCommand(Guid? CaseId, InvestigationOperation Operation, string IdempotencyKey,
    InvestigationCreateRequest? Create = null, InvestigationActionRequest? Action = null)
    : ICommand<InvestigationResponse>, ITransactionalCommand<WaslaWritePersistence>;
public interface IInvestigationService
{
    Task<Result<ReportPage<InvestigationResponse>>> ListAsync(InvestigationFilter filter, CancellationToken ct);
    Task<Result<InvestigationResponse>> DetailAsync(Guid id, CancellationToken ct);
    Task<Result<ReportPage<InvestigationHistoryResponse>>> HistoryAsync(Guid id, int pageNumber, int pageSize, CancellationToken ct);
    Task<Result<InvestigationFilterOptions>> OptionsAsync(CancellationToken ct, ReportOptionsFilter? filter = null);
    Task<Result<InvestigationResponse>> WriteAsync(WriteInvestigationCommand command, CancellationToken ct);
}
internal sealed class WriteInvestigationHandler(IInvestigationService service) : ICommandHandler<WriteInvestigationCommand, InvestigationResponse>
{
    public Task<Result<InvestigationResponse>> Handle(WriteInvestigationCommand request, CancellationToken cancellationToken)
        => service.WriteAsync(request, cancellationToken);
}
