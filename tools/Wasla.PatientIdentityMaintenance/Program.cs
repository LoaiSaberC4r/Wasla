using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlock.Application.Abstraction.Security;
using Wasla.Application;
using Wasla.Application.Features.PatientClaims;
using Wasla.Infrastructure;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer;

if (args.Length != 3 || args[0] is not ("hold" or "release") || !Guid.TryParse(args[1], out var claim))
{ Console.Error.WriteLine("Usage: hold|release <claim-id> <documented-reason>. Supply maintenance connection and operator credentials through environment variables."); return 2; }
var connection = Environment.GetEnvironmentVariable("WASLA_IDENTITY_MAINTENANCE_CONNECTION");
var identifier = Environment.GetEnvironmentVariable("WASLA_IDENTITY_OPERATOR_IDENTIFIER");
var password = Environment.GetEnvironmentVariable("WASLA_IDENTITY_OPERATOR_PASSWORD");
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password)) return 2;
var config = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connection }).Build();
var services = new ServiceCollection(); services.AddLogging(); services.AddWaslaApplication(); services.AddWaslaInfrastructure(config); services.AddWaslaSqlServerPersistence(config);
// The CLI has no authenticated HTTP principal. The operator command authenticates
// fresh credentials and checks persisted permissions before touching a claim.
services.AddSingleton<ICurrentUser, MaintenanceExecutionContext>();
await using var provider = services.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SetPatientClaimLegalHoldCommand(identifier, password, claim, args[0] == "hold", args[2]));
Console.WriteLine(result.IsSuccess ? "Legal hold operation committed and audited." : "Legal hold operation denied or unavailable."); return result.IsSuccess ? 0 : 1;
