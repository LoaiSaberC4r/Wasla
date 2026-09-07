using BuildingBlock.Api.Bootstrap;
using BuildingBlock.Api.Logging;
using BuildingBlock.Api.OpenApi;
using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Api.Security;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Bootstrap;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace BuildingBlock.Tests;

public sealed class Phase4PublicApiV2Tests
{
    [Fact]
    public async Task Result_invariants_and_composition_are_preserved()
    {
        var error = Error.Domain("Common.Domain", "The operation failed.");

        Assert.Throws<ArgumentException>(() => Result.Fail(Array.Empty<Error>()));
        Assert.Throws<ArgumentException>(() => Result.Fail(new Error[] { null! }));
        Assert.Throws<ArgumentNullException>(() => Result.Fail((Error)null!));
        Assert.Throws<InvalidOperationException>(() => _ = Result<int>.Fail(error).Value);
        Assert.Empty(Result.Ok().Errors);

        var firstFailure = Result.FirstFailureOrOk(Result.Ok(), Result.Fail(error), Result.Fail(Error.Unknown("Common.Other", "Other")));
        Assert.Same(error, Assert.Single(firstFailure.Errors));

        var success = Result<int>.Ok(4);
        Assert.Equal(8, success.Map(value => value * 2).Value);
        Assert.Equal("4", success.Bind(value => Result<string>.Ok(value.ToString(CultureInfo.InvariantCulture))).Value);
        Assert.True(success.Ensure(value => value > 0, error).IsSuccess);
        Assert.True(success.Ensure(value => value < 0, error).IsFailure);
        Assert.Equal("value:4", success.Match(value => $"value:{value}", _ => "failed"));

        var mapped = await success.MapAsync(value => ValueTask.FromResult(value + 1));
        var bound = await success.BindAsync(value => ValueTask.FromResult(Result<int>.Ok(value + 2)));
        var matched = await success.MatchAsync(
            value => ValueTask.FromResult(value + 3),
            _ => ValueTask.FromResult(-1));

        Assert.Equal(5, mapped.Value);
        Assert.Equal(6, bound.Value);
        Assert.Equal(7, matched);
        var appendedErrors = firstFailure.Errors.Add(error);
        Assert.Single(firstFailure.Errors);
        Assert.Equal(2, appendedErrors.Length);
    }

    [Fact]
    public void Paged_result_uses_long_safe_totals_and_copies_items()
    {
        var source = new List<int> { 1, 2 };
        var page = new PagedResult<int>(2, 2, 5, source);
        source.Add(3);

        Assert.Equal(2, page.PageNumber);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5L, page.TotalItems);
        Assert.Equal(3L, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
        Assert.Equal(new[] { 1, 2 }, page.Items);

        Assert.Equal(long.MaxValue, new PagedResult<int>(1, 1, long.MaxValue, []).TotalPages);
        Assert.Equal(0L, new PagedResult<int>(1, 10, 0, []).TotalPages);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PagedResult<int>(0, 10, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PagedResult<int>(1, 0, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PagedResult<int>(1, 10, -1, []));
        Assert.Throws<ArgumentNullException>(() => new PagedResult<int>(1, 10, 0, null!));
    }

    [Fact]
    public void Removed_v2_apis_and_obsolete_public_members_are_absent()
    {
        var assemblies = ApiAssemblies();
        var exportedTypes = assemblies.SelectMany(assembly => assembly.GetExportedTypes()).ToArray();
        var publicMembers = exportedTypes.SelectMany(type => type.GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).ToArray();

        Assert.DoesNotContain(exportedTypes, type => type.Namespace?.Contains(".Legacy", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(exportedTypes, type => type.Name is
            "Pagination`1" or
            "IEfDbContextProvider" or
            "SoftDeleteFilterState" or
            "ISoftDeleteFilterContext" or
            "SoftDeleteFilterDbContext" or
            "BuildingBlockQueryFilterNames" or
            "SpecificationEvaluator`1");
        Assert.DoesNotContain(publicMembers, member => member.Name is
            "ThrowIfFailure" or
            "CountAsync" or
            "ListWithCountAsync" or
            "QueryTag" or
            "AddingEmailService" or
            "AddMediaService" or
            "AddQrCodeService" or
            "AddSecurity" or
            "AddBuildingBlockAuditingAndSoftDelete");
        Assert.DoesNotContain(exportedTypes, type => type.GetCustomAttribute<ObsoleteAttribute>() is not null);
        Assert.DoesNotContain(publicMembers, member => member.GetCustomAttribute<ObsoleteAttribute>() is not null);
    }

    [Fact]
    public void Repository_contracts_keep_query_and_save_boundaries_explicit()
    {
        var readMethods = typeof(IReadRepository<,>).GetMethods();
        var writeMethods = typeof(IWriteRepository<,>).GetMethods();
        var unitOfWorkMethods = typeof(IUnitOfWork<>).GetMethods();

        Assert.Contains(readMethods, method => method.Name == "LongCountAsync" && method.ReturnType == typeof(Task<long>));
        Assert.Equal(2, readMethods.Count(method => method.Name == "ListWithLongCountAsync"));
        Assert.DoesNotContain(readMethods.Concat(writeMethods), method => method.Name.Contains("SaveChanges", StringComparison.Ordinal));
        Assert.Contains(unitOfWorkMethods, method => method.Name == "SaveChangesAsync");
        Assert.Contains(unitOfWorkMethods, method => method.Name == "BeginTransactionAsync");
        Assert.Contains(typeof(IAggregateRoot), typeof(IWriteRepository<,>).GetGenericArguments()[0].GetGenericParameterConstraints());
        Assert.DoesNotContain(typeof(IAggregateRoot), typeof(IReadRepository<,>).GetGenericArguments()[0].GetGenericParameterConstraints());
    }

    [Fact]
    public void Application_public_api_does_not_expose_provider_query_types()
    {
        var forbiddenDefinitions = new[]
        {
            typeof(IQueryable<>),
            typeof(DbSet<>),
            typeof(DbContext)
        };

        var exposedTypes = typeof(IReadPersistenceMarker).Assembly
            .GetExportedTypes()
            .SelectMany(PublicSignatureTypes)
            .SelectMany(FlattenType)
            .ToArray();

        Assert.DoesNotContain(exposedTypes, type =>
            forbiddenDefinitions.Any(forbidden =>
                type == forbidden ||
                type.IsGenericType && type.GetGenericTypeDefinition() == forbidden));
    }

    [Fact]
    public void Dependency_injection_extension_names_follow_the_v2_convention()
    {
        var extensionContainers = new[]
        {
            typeof(BuildingBlock.Application.Bootstrap.DependencyInjection),
            typeof(BuildingBlock.Infrastructure.Bootstrap.DependencyInjection),
            typeof(LocalizationExtensions),
            typeof(SerilogBootstrapper),
            typeof(BuildingBlock.Api.OpenApi.SwaggerServiceCollectionExtensions),
            typeof(BuildingBlock.Api.ProblemDetails.ProblemDetailsServiceCollectionExtensions),
            typeof(SecurityServiceCollectionExtensions),
            typeof(SqlServerExceptionMappingServiceCollectionExtensions)
        };

        var extensionMethods = extensionContainers
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.IsDefined(typeof(ExtensionAttribute), inherit: false))
            .ToArray();

        Assert.NotEmpty(extensionMethods);
        Assert.All(extensionMethods, method => Assert.Matches("^(Add|Use|Map)BuildingBlock", method.Name));
    }

    private static Assembly[] ApiAssemblies()
        =>
        [
            typeof(IAggregateRoot).Assembly,
            typeof(IReadPersistenceMarker).Assembly,
            typeof(BuildingBlock.Infrastructure.Persistence.ModelBuilderConfigExtensions).Assembly,
            typeof(BuildingBlock.Api.ProblemDetails.IProblemDetailsMapper).Assembly,
            typeof(SqlServerExceptionMappingServiceCollectionExtensions).Assembly
        ];

    private static IEnumerable<Type> PublicSignatureTypes(Type type)
    {
        yield return type;

        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return property.PropertyType;
        }
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            foreach (var nested in FlattenType(elementType))
            {
                yield return nested;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in FlattenType(argument))
            {
                yield return nested;
            }
        }
    }
}
