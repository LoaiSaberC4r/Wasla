using BuildingBlock.Application.Time;
using Microsoft.AspNetCore.Http;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class MedicalRecordAuditWriter(WaslaDbContext db, IDateTimeProvider clock, IHttpContextAccessor http)
{
    public async Task WriteAsync(MedicalRecordScope scope, string action, string resource, Guid? source, Guid? encounter, CancellationToken ct)
    {
        db.Set<MedicalRecordAccessAudit>().Add(MedicalRecordAccessAudit.Create(scope.ActorId, scope.PatientId,
            scope.Mode == MedicalRecordMode.TreatingDoctor && scope.TicketId is null ? "OwnDoctor" : scope.Mode.ToString(), action, resource, source, encounter, scope.TicketId, http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"), clock.UtcNow));
        // Failure propagates through the Result/ProblemDetails pipeline; no protected response is released.
        try { await db.SaveChangesAsync(ct); }
        catch { MedicalRecordTelemetry.AuditFailed(); throw; }
    }
}
