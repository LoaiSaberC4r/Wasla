using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;
internal sealed class HistoricalDocumentOptionsValidator(IHostEnvironment environment) : IValidateOptions<HistoricalDocumentOptions>
{
    public ValidateOptionsResult Validate(string? name, HistoricalDocumentOptions options)
        => environment.IsProduction() && !options.RequireRealScanner
            ? ValidateOptionsResult.Fail("Historical documents require real malware scanning in Production.") : ValidateOptionsResult.Success;
}
