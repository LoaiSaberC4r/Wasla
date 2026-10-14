using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using MediatR;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Families;
using Wasla.Application.Features.Finance;
using Wasla.Application.Features.Patients;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Features.Tickets.GetMyActiveTickets;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Dashboards;

public sealed record PatientCapability(string Code, string NameAr, string NameEn, bool Available, string? Route, string? UnavailableReasonCode = null);
public sealed record PatientDashboardCapabilities(Guid PatientId, DateTime GeneratedAtUtc, IReadOnlyList<PatientCapability> Capabilities,
    bool CrossDoctorSharingEnabled, bool GuardianSharingEnabled, IReadOnlyList<string> Warnings);
public sealed record PatientDashboardOverview(Guid PatientId, DateTime GeneratedAtUtc, PatientProfileResponse? Profile,
    ReservationPagedResponse? UpcomingBookings, IReadOnlyList<MyActiveTicketResponse> ActiveTickets,
    RecordSummary? MedicalSummary, RecordPage<TimelineEntry>? RecentPermittedTimeline,
    ClinicalPage<FollowUpEligibilityResponse>? FollowUpAvailability, FinancialPage<PatientFinancialTransactionItem>? RecentFinancialTransactions,
    MyFamilyResponse? Family, PatientDashboardCapabilities Capabilities, IReadOnlyList<string> Warnings);
public sealed record GetPatientDashboardOverviewQuery : IQuery<PatientDashboardOverview>;
public sealed record GetPatientDashboardCapabilitiesQuery : IQuery<PatientDashboardCapabilities>;
public sealed class PatientDashboardComposer(IReportingAccessGuard guard, ISender sender, IFinanceReadService finance,
    IMedicalRecordAccessPolicy medicalAccess, IFamilyMedicalAccessPolicy families, IOptions<MedicalRecordOptions> options, IDateTimeProvider clock)
{
    public async Task<Result<PatientDashboardCapabilities>> CapabilitiesAsync(CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.Patient, Phase18PermissionNames.PatientDashboard, null, null, ct);
        if (access.IsFailure) return Result<PatientDashboardCapabilities>.Fail(access.Errors);
        var scope = access.Value;
        var medical = await medicalAccess.ScopeAsync(MedicalRecordMode.OwnPatient, null, null, null, PermissionNames.MedicalRecordsViewOwn, ct);
        var log = await medicalAccess.ScopeAsync(MedicalRecordMode.OwnPatient, null, null, null, PermissionNames.MedicalRecordsViewOwnAccessLog, ct);
        PatientCapability Make(string code, string ar, string en, string permission, string route)
        {
            var allowed = scope.Permissions.Contains(permission);
            return new(code, ar, en, allowed, allowed ? route : null, allowed ? null : "Dashboard.PermissionRequired");
        }
        var guardian = options.Value.GuardianSharingEnabled;
        var eligible = guardian ? await families.DependentsAsync(ct) : null;
        var familyMedicalAvailable = eligible?.IsSuccess == true;
        return Result<PatientDashboardCapabilities>.Ok(new(scope.PatientId!.Value, clock.UtcNow,
            [Make("Profile", "ملفي", "My profile", PermissionNames.PatientProfileViewOwn, "/api/v1/patients/me"),
                Make("Reservations", "حجوزاتي", "My bookings", PermissionNames.ReservationsViewOwn, "/api/v1/reservations/mine"),
                Make("Queue", "تذاكري النشطة", "My active queue", PermissionNames.TicketsViewOwn, "/api/v1/tickets/mine/active"),
                new("MedicalRecord", "سجلي الطبي", "My medical record", medical.IsSuccess, medical.IsSuccess ? "/api/v1/patients/me/medical-record/overview" : null,
                    medical.IsSuccess ? null : "Dashboard.MedicalCapabilityUnavailable"),
                new("AccessLog", "سجل الوصول", "My access log", log.IsSuccess, log.IsSuccess ? "/api/v1/patients/me/medical-record/access-log" : null,
                    log.IsSuccess ? null : "Dashboard.MedicalCapabilityUnavailable"),
                Make("Prescriptions", "وصفاتي", "My prescriptions", PermissionNames.PrescriptionsViewOwnCompleted, "/api/v1/prescriptions/mine"),
                Make("LabRequests", "طلبات التحاليل", "My lab requests", PermissionNames.LabRequestsViewOwnIssued, "/api/v1/lab-requests/mine"),
                Make("LabResults", "نتائج التحاليل", "My lab results", PermissionNames.LabResultsViewOwnCurrent, "/api/v1/lab-results/mine"),
                Make("RadiologyRequests", "طلبات الأشعة", "My radiology requests", PermissionNames.RadiologyRequestsViewOwnIssued, "/api/v1/radiology-requests/mine"),
                Make("RadiologyResults", "نتائج الأشعة", "My radiology results", PermissionNames.RadiologyResultsViewOwnCurrent, "/api/v1/radiology-results/mine"),
                Make("FinancialHistory", "مدفوعاتي وإيصالاتي", "My payments and receipts", PermissionNames.PaymentsViewOwn, "/api/v1/patients/me/financial-transactions"),
                Make("FollowUp", "متابعة الزيارة", "Follow-up availability", PermissionNames.FollowUpEligibilityViewBookingEligibility, "/api/v1/follow-up-eligibilities/mine"),
                Make("Family", "أسرتي", "My family", PermissionNames.FamiliesViewOwn, "/api/v1/families/mine"),
                new("FamilyMedicalEligibility", "أهلية الوصول الطبي للأسرة", "Family medical eligibility", familyMedicalAvailable,
                    familyMedicalAvailable ? "/api/v1/families/mine/medical-record-dependents" : null,
                    guardian ? "Dashboard.EligibilityIsRecheckedForEachDependent" : "FamilyMedicalAccess.FeatureDisabled")],
            options.Value.CrossDoctorSharingEnabled, guardian,
            ["Dashboard.FamilyMembershipDoesNotGrantClinicalAccess", "Dashboard.AttachmentsAreReauthorizedOnEveryRead", "Dashboard.QueueHasNoGuaranteedWaitingTime"]));
    }
    public async Task<Result<PatientDashboardOverview>> OverviewAsync(CancellationToken ct)
    {
        var capabilities = await CapabilitiesAsync(ct); if (capabilities.IsFailure) return Result<PatientDashboardOverview>.Fail(capabilities.Errors);
        bool Can(string code) => capabilities.Value.Capabilities.Single(c => c.Code == code).Available;
        List<string> warnings = [.. capabilities.Value.Warnings];
        PatientProfileResponse? profile = null; ReservationPagedResponse? bookings = null; IReadOnlyList<MyActiveTicketResponse> tickets = [];
        RecordSummary? summary = null; RecordPage<TimelineEntry>? timeline = null; ClinicalPage<FollowUpEligibilityResponse>? followup = null;
        FinancialPage<PatientFinancialTransactionItem>? transactions = null; MyFamilyResponse? family = null;
        if (Can("Profile"))
        {
            var r = await sender.Send(new GetMyPatientProfileQuery(), ct); if (r.IsSuccess) profile = r.Value; else warnings.Add("Dashboard.ProfileUnavailable");
        }
        if (Can("Reservations"))
        {
            // Supplying the resolved self ID prevents the existing family booking list from broadening this dashboard.
            var r = await sender.Send(new ListMineReservationsQuery(capabilities.Value.PatientId, "Upcoming", null, null, null, 1, 5), ct);
            if (r.IsSuccess) bookings = r.Value; else warnings.Add("Dashboard.BookingsUnavailable");
        }
        if (Can("Queue"))
        {
            var r = await sender.Send(new GetMyActiveTicketsQuery(), ct); if (r.IsSuccess) tickets = r.Value; else warnings.Add("Dashboard.QueueUnavailable");
        }
        if (Can("MedicalRecord"))
        {
            var r = await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Overview, MedicalRecordMode.OwnPatient, Filter: new(PageSize: 5)), ct);
            if (r.IsSuccess && r.Value is PatientSafeMedicalRecordOverviewResponse safe) { summary = safe.Summary; timeline = safe.Timeline; }
            else warnings.Add("Dashboard.MedicalRecordUnavailable");
        }
        if (Can("FollowUp"))
        {
            var r = await sender.Send(new ListMyFollowUpEligibilitiesQuery(capabilities.Value.PatientId, FollowUpEligibilityStatus.Available, PageSize: 5), ct);
            if (r.IsSuccess) followup = r.Value; else warnings.Add("Dashboard.FollowUpUnavailable");
        }
        if (Can("FinancialHistory"))
        {
            var r = await finance.ListOwnTransactionsAsync(new(PageSize: 5), ct); if (r.IsSuccess) transactions = r.Value; else warnings.Add("Dashboard.FinancialHistoryUnavailable");
        }
        if (Can("Family"))
        {
            var r = await sender.Send(new GetMyFamilyQuery(), ct);
            if (r.IsSuccess) family = r.Value; else warnings.Add("Dashboard.NoAccessibleFamily");
        }
        return Result<PatientDashboardOverview>.Ok(new(capabilities.Value.PatientId, clock.UtcNow, profile, bookings, tickets,
            summary, timeline, followup, transactions, family, capabilities.Value, warnings));
    }
}
internal sealed class GetPatientDashboardOverviewHandler(PatientDashboardComposer composer) : IQueryHandler<GetPatientDashboardOverviewQuery, PatientDashboardOverview>
{
    public Task<Result<PatientDashboardOverview>> Handle(GetPatientDashboardOverviewQuery request, CancellationToken ct) => composer.OverviewAsync(ct);
}
internal sealed class GetPatientDashboardCapabilitiesHandler(PatientDashboardComposer composer) : IQueryHandler<GetPatientDashboardCapabilitiesQuery, PatientDashboardCapabilities>
{
    public Task<Result<PatientDashboardCapabilities>> Handle(GetPatientDashboardCapabilitiesQuery request, CancellationToken ct) => composer.CapabilitiesAsync(ct);
}
