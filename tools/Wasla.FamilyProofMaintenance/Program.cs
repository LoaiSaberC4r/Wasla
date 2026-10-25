using System.Text.Json;
using BuildingBlock.Application.Abstraction.Security;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application;
using Wasla.Application.Features.Families;
using Wasla.Infrastructure;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer;

if (args.Length != 1 || args[0] is not ("report" or "apply"))
{ Console.Error.WriteLine("Usage: report|apply. Supply WASLA_FAMILY_MAINTENANCE_CONNECTION, WASLA_FAMILY_OPERATOR_IDENTIFIER, WASLA_FAMILY_OPERATOR_PASSWORD and signed FamilyMedicalPolicy settings through environment variables."); return 2; }
var connection = Environment.GetEnvironmentVariable("WASLA_FAMILY_MAINTENANCE_CONNECTION");
var identifier = Environment.GetEnvironmentVariable("WASLA_FAMILY_OPERATOR_IDENTIFIER");
var password = Environment.GetEnvironmentVariable("WASLA_FAMILY_OPERATOR_PASSWORD");
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password)) return 2;
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connection }).Build();
var services = new ServiceCollection(); services.AddLogging(); services.AddWaslaApplication(); services.AddWaslaInfrastructure(configuration); services.AddWaslaSqlServerPersistence(configuration);
services.AddSingleton<ICurrentUser, MaintenanceContext>();
await using var provider = services.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReconcileFamilyRelationshipProofsCommand(identifier, password, args[0] == "apply"));
Console.WriteLine(result.IsSuccess ? JsonSerializer.Serialize(result.Value) : "Reconciliation denied or unavailable.");
return result.IsSuccess ? 0 : 1;

internal sealed class MaintenanceContext : ICurrentUser
{
    public bool IsAuthenticated => false;
    public Guid? UserId => null;
    public string? UserName => null;
    public string? Email => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
    public string? GetClaimValue(string claimType) => null;
    public IReadOnlyCollection<string> GetClaimValues(string claimType) => [];
}
