using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.ReferenceData;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed record GovernorateSeed(int Id, string NameAr, string? NameEn, int DisplayOrder);
internal sealed record CitySeed(int Id, int GovernorateId, string NameAr, string? NameEn, int DisplayOrder);
internal sealed record AreaSeed(int Id, int CityId, string NameAr, string? NameEn, int DisplayOrder);
internal sealed record EgyptLocationSeedData(
    IReadOnlyList<GovernorateSeed> Governorates,
    IReadOnlyList<CitySeed> Cities,
    IReadOnlyList<AreaSeed> Areas);

internal sealed class EgyptLocationSeedDataException(string message, Exception? inner = null) : Exception(message, inner);
internal sealed class EgyptLocationSeedConflictException(string message) : Exception(message);

internal static class EgyptLocationSeedCatalog
{
    public const int ExpectedGovernorateCount = 27;
    public const int ExpectedCityCount = 351;
    public const int ExpectedAreaCount = 5716;

    private static readonly Lazy<EgyptLocationSeedData> Catalog = new(LoadAndValidate, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static EgyptLocationSeedData Data => Catalog.Value;

    private static EgyptLocationSeedData LoadAndValidate()
    {
        const string prefix = "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Seeding.Data.";
        var assembly = typeof(EgyptLocationSeedCatalog).Assembly;
        var data = new EgyptLocationSeedData(
            Load<GovernorateSeed>(assembly, prefix + "egypt-governorates.v1.json"),
            Load<CitySeed>(assembly, prefix + "egypt-cities.v1.json"),
            Load<AreaSeed>(assembly, prefix + "egypt-areas.v1.json"));
        EgyptLocationSeedValidator.Validate(data);
        return data;
    }

    private static ReadOnlyCollection<T> Load<T>(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new EgyptLocationSeedDataException($"Missing embedded Egypt seed resource '{resourceName}'.");
        try
        {
            return Array.AsReadOnly(JsonSerializer.Deserialize<T[]>(stream, SerializerOptions)
                ?? throw new EgyptLocationSeedDataException($"Egypt seed resource '{resourceName}' is null."));
        }
        catch (JsonException exception)
        {
            throw new EgyptLocationSeedDataException($"Egypt seed resource '{resourceName}' is invalid.", exception);
        }
    }
}

internal static class EgyptLocationSeedValidator
{
    public static void Validate(EgyptLocationSeedData data)
    {
        if (data.Governorates.Count != EgyptLocationSeedCatalog.ExpectedGovernorateCount ||
            data.Cities.Count != EgyptLocationSeedCatalog.ExpectedCityCount ||
            data.Areas.Count != EgyptLocationSeedCatalog.ExpectedAreaCount)
        {
            throw new EgyptLocationSeedDataException("The frozen Egypt location catalog counts do not match the approved v1 counts.");
        }

        ValidateCommon(data.Governorates.Select(item => (item.Id, item.NameAr, item.NameEn, item.DisplayOrder)), "governorate");
        ValidateCommon(data.Cities.Select(item => (item.Id, item.NameAr, item.NameEn, item.DisplayOrder)), "city");
        ValidateCommon(data.Areas.Select(item => (item.Id, item.NameAr, item.NameEn, item.DisplayOrder)), "area");

        var governorateIds = data.Governorates.Select(item => item.Id).ToHashSet();
        var cityIds = data.Cities.Select(item => item.Id).ToHashSet();
        if (data.Cities.Any(item => !governorateIds.Contains(item.GovernorateId) || item.Id != item.GovernorateId * 1000 + item.DisplayOrder))
        {
            throw new EgyptLocationSeedDataException("The Egypt city catalog has an invalid parent or stable ID.");
        }

        if (data.Areas.Any(item => !cityIds.Contains(item.CityId) || item.Id != (long)item.CityId * 10000 + item.DisplayOrder))
        {
            throw new EgyptLocationSeedDataException("The Egypt area catalog has an invalid parent or stable ID.");
        }

        EnsureUnique(data.Governorates.Select(item => Normalize(item.NameAr)), "governorate Arabic name");
        EnsureUnique(data.Governorates.Select(item => Normalize(item.NameEn)), "governorate English name");
        EnsureUnique(data.Cities.Select(item => $"{item.GovernorateId}:{Normalize(item.NameAr)}"), "city Arabic name");
        EnsureUnique(data.Cities.Select(item => $"{item.GovernorateId}:{Normalize(item.NameEn)}"), "city English name");
        EnsureUnique(data.Areas.Select(item => $"{item.CityId}:{Normalize(item.NameAr)}"), "area Arabic name");
        EnsureUnique(data.Areas.Select(item => $"{item.CityId}:{Normalize(item.NameEn)}"), "area English name");
    }

    private static void ValidateCommon(IEnumerable<(int Id, string NameAr, string? NameEn, int DisplayOrder)> values, string level)
    {
        var records = values.ToArray();
        if (records.Any(item => item.Id <= 0 || item.DisplayOrder <= 0 || string.IsNullOrWhiteSpace(item.NameAr) ||
                                item.NameAr != item.NameAr.Trim() || item.NameAr.Length > 150 ||
                                item.NameEn is { Length: > 150 } || item.NameEn is not null && item.NameEn != item.NameEn.Trim()))
        {
            throw new EgyptLocationSeedDataException($"An Egypt {level} seed record is invalid.");
        }

        EnsureUnique(records.Select(item => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), $"{level} ID");
    }

    private static void EnsureUnique(IEnumerable<string> values, string field)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values.Any(value => !set.Add(value)))
        {
            throw new EgyptLocationSeedDataException($"The Egypt catalog contains a duplicate {field}.");
        }
    }

    internal static string Normalize(string? value)
        => (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
}

internal sealed class EgyptLocationSeedCoordinator(WaslaDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var catalog = EgyptLocationSeedCatalog.Data;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await SeedGovernoratesAsync(catalog.Governorates, cancellationToken);
            await SeedCitiesAsync(catalog.Cities, cancellationToken);
            await SeedAreasAsync(catalog.Areas, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task SeedGovernoratesAsync(IReadOnlyList<GovernorateSeed> seeds, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Governorates.AsNoTracking().ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(item => item.Id);
        foreach (var seed in seeds)
        {
            if (byId.TryGetValue(seed.Id, out var current))
            {
                Ensure(current.NameAr == seed.NameAr && current.NameEn == seed.NameEn, "Governorate seed ID conflicts with existing names.");
                continue;
            }

            Ensure(!existing.Any(item => Same(item.NameAr, seed.NameAr) || Same(item.NameEn, seed.NameEn)), "Governorate seed name conflicts with a different ID.");
            dbContext.Governorates.Add(Governorate.Create(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder).Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedCitiesAsync(IReadOnlyList<CitySeed> seeds, CancellationToken cancellationToken)
    {
        var knownParents = await dbContext.Governorates.AsNoTracking().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existing = await dbContext.Cities.AsNoTracking().ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(item => item.Id);
        foreach (var seed in seeds)
        {
            Ensure(knownParents.Contains(seed.GovernorateId), "City seed references a missing Governorate.");
            if (byId.TryGetValue(seed.Id, out var current))
            {
                Ensure(current.GovernorateId == seed.GovernorateId && current.NameAr == seed.NameAr && current.NameEn == seed.NameEn,
                    "City seed ID conflicts with an existing parent or name.");
                continue;
            }

            Ensure(!existing.Any(item => item.GovernorateId == seed.GovernorateId &&
                                         (Same(item.NameAr, seed.NameAr) || Same(item.NameEn, seed.NameEn))),
                "City seed name conflicts under the same Governorate.");
            dbContext.Cities.Add(City.Create(seed.Id, seed.GovernorateId, seed.NameAr, seed.NameEn, seed.DisplayOrder).Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAreasAsync(IReadOnlyList<AreaSeed> seeds, CancellationToken cancellationToken)
    {
        var knownParents = await dbContext.Cities.AsNoTracking().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existing = await dbContext.Areas.AsNoTracking().ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(item => item.Id);
        foreach (var seed in seeds)
        {
            Ensure(knownParents.Contains(seed.CityId), "Area seed references a missing City.");
            if (byId.TryGetValue(seed.Id, out var current))
            {
                Ensure(current.CityId == seed.CityId && current.NameAr == seed.NameAr && current.NameEn == seed.NameEn,
                    "Area seed ID conflicts with an existing parent or name.");
                continue;
            }

            Ensure(!existing.Any(item => item.CityId == seed.CityId &&
                                         (Same(item.NameAr, seed.NameAr) || Same(item.NameEn, seed.NameEn))),
                "Area seed name conflicts under the same City.");
            dbContext.Areas.Add(Area.Create(seed.Id, seed.CityId, seed.NameAr, seed.NameEn, seed.DisplayOrder).Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool Same(string? left, string? right)
        => left is not null && right is not null && string.Equals(
            EgyptLocationSeedValidator.Normalize(left),
            EgyptLocationSeedValidator.Normalize(right),
            StringComparison.OrdinalIgnoreCase);

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new EgyptLocationSeedConflictException(message);
        }
    }
}
