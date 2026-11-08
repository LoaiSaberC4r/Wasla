using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;
internal sealed class HistoricalDocumentOptionsValidator : IValidateOptions<HistoricalDocumentOptions>
{
    public ValidateOptionsResult Validate(string? name, HistoricalDocumentOptions options)
        // The temporary Production exception is controlled by RequireRealScanner.
        => ValidateOptionsResult.Success;
}
