using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wasla.Application.Features.Archives;
using Wasla.Application.Features.Reporting;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

// The Phase 12 component fixtures supply their own authorization actor and scope.
// Full API/security tests exercise the production database-backed guard separately.
internal static class Phase12ArchiveTestServices
{
    internal static void Add(IServiceCollection services)
    {
        services.AddLogging();
        services.TryAddScoped(typeof(Wasla.Application.Persistence.IWaslaDataStore), typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDataStore")!);
        services.AddScoped(typeof(Wasla.Application.Persistence.IPatientIdentityStore), typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.PatientIdentityStore")!);
        services.AddScoped(typeof(IArchiveLocationResolver), typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.ArchiveLocationResolver")!);
        services.AddScoped<IReportingAccessGuard, ComponentGuard>();
        services.AddScoped(typeof(Wasla.Application.Features.DoctorPatients.IDoctorPatientReadService), typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.DoctorPatientReadService")!);
    }

    private sealed class ComponentGuard(ICurrentUser user) : IReportingAccessGuard
    {
        public Task<Result<ReportScope>> AuthorizeAsync(ReportActor actor, string permission,
            Guid? practiceId, Guid? doctorId, CancellationToken cancellationToken)
            => Task.FromResult(user.IsAuthenticated && user.UserId is { } id && user.Permissions.Contains(permission)
                ? Result<ReportScope>.Ok(new(actor, id, doctorId, null,
                    practiceId is { } practice ? [practice] : [], user.Permissions.ToArray()))
                : Result<ReportScope>.Fail(ReportingErrors.Denied));
    }
}
