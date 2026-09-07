using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer;
using BuildingBlock.Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BuildingBlock.Tests;

public sealed class SqlServerExceptionMapperTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(2601)]
    [InlineData(2627)]
    public void Duplicate_key_errors_map_to_conflict_without_sql_details(int number)
    {
        var mapper = new SqlServerExceptionToErrorMapper();
        var sqlException = CreateSqlException(
            number,
            "Cannot insert duplicate key row in object 'dbo.SecretTable' with unique index 'IX_Secret'.");

        Assert.True(mapper.TryMap(new DbUpdateException("wrapper", sqlException), out var error));

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(ErrorCodes.Persistence.UniqueConstraint, error.Code);
        Assert.DoesNotContain("SecretTable", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IX_Secret", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(-2, ErrorCodes.Persistence.Timeout)]
    [InlineData(1205, ErrorCodes.Persistence.Deadlock)]
    [InlineData(40613, ErrorCodes.Persistence.Unavailable)]
    public void Provider_specific_transient_errors_map_safely(int number, string expectedCode)
    {
        var mapper = new SqlServerExceptionToErrorMapper();
        var sqlException = CreateSqlException(number, "Raw provider text with server and database names.");

        Assert.True(mapper.TryMap(sqlException, out var error));

        Assert.Equal(ErrorType.Infrastructure, error.Type);
        Assert.Equal(expectedCode, error.Code);
        Assert.NotNull(error.RetryAfter);
        Assert.DoesNotContain("database", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Concurrency_conflicts_are_mapped_by_generic_ef_mapper()
    {
        var mapper = new EfCoreExceptionToErrorMapper();

        Assert.True(mapper.TryMap(new DbUpdateConcurrencyException("row version mismatch"), out var error));

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(ErrorCodes.Persistence.Concurrency, error.Code);
        Assert.DoesNotContain("row version mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SqlException CreateSqlException(int number, string message)
    {
        var errorCollection = (SqlErrorCollection)Activator.CreateInstance(
            typeof(SqlErrorCollection),
            nonPublic: true)!;
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
