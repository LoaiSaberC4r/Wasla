using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer;
using BuildingBlock.Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BuildingBlock.Tests;

public sealed class EfProviderSeparationTests
{
    [Fact]
    public void Generic_infrastructure_does_not_reference_sqlclient()
    {
        var references = typeof(EfCoreExceptionToErrorMapper).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name == "Microsoft.Data.SqlClient");
    }

    [Fact]
    public void Sql_server_mapping_assembly_references_sqlclient()
    {
        var references = typeof(SqlServerExceptionToErrorMapper).Assembly.GetReferencedAssemblies();

        Assert.Contains(references, reference => reference.Name == "Microsoft.Data.SqlClient");
    }

    [Fact]
    public void Generic_mapper_maps_concurrency_only()
    {
        var mapper = new EfCoreExceptionToErrorMapper();

        Assert.True(mapper.TryMap(new DbUpdateConcurrencyException(), out var error));
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(ErrorCodes.Persistence.Concurrency, error.Code);

        Assert.False(mapper.TryMap(new DbUpdateException("unknown update failure"), out _));
    }

    [Fact]
    public void Sql_server_mapper_maps_duplicate_key_without_raw_message()
    {
        var sqlException = CreateSqlException(2627, "Violation of UNIQUE KEY constraint 'AK_Secret'. Table dbo.Users.");
        var mapper = new SqlServerExceptionToErrorMapper();

        Assert.True(mapper.TryMap(new DbUpdateException("wrapper", sqlException), out var error));
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(ErrorCodes.Persistence.UniqueConstraint, error.Code);
        Assert.DoesNotContain("AK_Secret", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbo.Users", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SqlException CreateSqlException(int number, string message)
    {
        var errorCollection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        var errorConstructor = typeof(SqlError)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(ctor => ctor.GetParameters().Length == 9);
        var error = errorConstructor.Invoke(new object?[]
        {
            number,
            (byte)1,
            (byte)14,
            "server",
            message,
            "procedure",
            1,
            0,
            null
        });

        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(errorCollection, new[] { error });

        var exceptionConstructor = typeof(SqlException)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(ctor => ctor.GetParameters().Length == 4);

        return (SqlException)exceptionConstructor.Invoke(new object?[]
        {
            message,
            errorCollection,
            null,
            Guid.NewGuid()
        });
    }
}
