using Microsoft.EntityFrameworkCore;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

// Controlled schema-preparation utility. It never moves/deletes operational rows,
// executes migrations, starts hosted services or activates physical archival.
if (args.Length != 1 || args[0] != "populate-registries")
{
    Console.Error.WriteLine("Usage: populate-registries; configure WASLA_ARCHIVE_MAINTENANCE_CONNECTION separately.");
    return 2;
}
var connection = Environment.GetEnvironmentVariable("WASLA_ARCHIVE_MAINTENANCE_CONNECTION");
if (string.IsNullOrWhiteSpace(connection)) { Console.Error.WriteLine("Maintenance connection is required."); return 2; }
await using var db = new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(connection).Options);
try
{
    var population = new ArchiveRegistryPopulation(db);
    var processed = 0;
    for (var batch = 0; batch < 4; batch++)
    {
        var count = await population.PopulateAsync(250);
        processed += count;
        if (count == 0) break;
    }
    Console.WriteLine($"Registered identities in bounded transactions: {processed}. Rerun until zero before the guarded FK cutover.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Registry preparation stopped safely: {exception.GetType().Name}. Review constraint/coverage metadata; no operational rows were removed.");
    return 1;
}
