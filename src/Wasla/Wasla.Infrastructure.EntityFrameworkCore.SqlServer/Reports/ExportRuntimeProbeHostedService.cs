using Microsoft.Extensions.Hosting;
using Wasla.Application.Features.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

internal sealed class ExportRuntimeProbeHostedService(IReportDocumentRenderer renderer) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => renderer.ProbeAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
