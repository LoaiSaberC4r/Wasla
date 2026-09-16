using Microsoft.Data.SqlClient;

namespace Wasla.Tests.Integration;

/// <summary>
/// These tests deliberately use SQL Server rather than SQLite. They exercise
/// the transaction-owned application-lock protocol used by reservation writes.
/// CI supplies WASLA_SQLSERVER_CONNECTION_STRING.
/// </summary>
public sealed class ReservationSqlServerConcurrencyTests
{
    public static TheoryData<string, string> ReservationContentionScenarios => new()
    {
        { "same-slot", "Wasla:Reservation:Practice:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:20260920" },
        { "final-daily-capacity", "Wasla:Reservation:Practice:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb:20260920" },
        { "segment-capacity", "Wasla:Reservation:Practice:cccccccccccccccccccccccccccccccc:20260920" },
        { "patient-overlap", "Wasla:Reservation:Patient:dddddddddddddddddddddddddddddddd" },
        { "concurrent-reschedule", "Wasla:Reservation:Practice:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee:20260920" },
        { "cancel-vs-reschedule", "Wasla:Reservation:Patient:ffffffffffffffffffffffffffffffff" },
        { "idempotency", "Wasla:Reservation:Idempotency:11111111111111111111111111111111:Create:TEST" },
        { "reference-collision", "Wasla:Reservation:Reference:WSL-R-COLLIDE" }
    };

    [Theory]
    [MemberData(nameof(ReservationContentionScenarios))]
    [Trait("Category", "SQLServerConcurrency")]
    public async Task Transaction_owned_application_lock_allows_exactly_one_business_claim(
        string scenario,
        string resource)
    {
        var connectionString = Environment.GetEnvironmentVariable("WASLA_SQLSERVER_CONNECTION_STRING");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString),
            "WASLA_SQLSERVER_CONNECTION_STRING is required for SQL Server concurrency tests.");

        await using var setup = new SqlConnection(connectionString);
        await setup.OpenAsync(TestContext.Current.CancellationToken);
        await using (var command = setup.CreateCommand())
        {
            command.CommandText = """
                IF OBJECT_ID('dbo.ReservationConcurrencyClaims', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ReservationConcurrencyClaims
                    (
                        Scenario nvarchar(100) NOT NULL PRIMARY KEY,
                        ClaimedOnUtc datetime2(3) NOT NULL
                    );
                END;
                DELETE FROM dbo.ReservationConcurrencyClaims WHERE Scenario = @scenario;
                """;
            command.Parameters.AddWithValue("@scenario", scenario);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = TryClaimAsync(connectionString!, scenario, resource, gate.Task);
        var second = TryClaimAsync(connectionString!, scenario, resource, gate.Task);
        gate.SetResult();
        var outcomes = await Task.WhenAll(first, second);

        Assert.Equal(1, outcomes.Count(outcome => outcome));
        Assert.Equal(1, outcomes.Count(outcome => !outcome));
    }

    private static async Task<bool> TryClaimAsync(
        string connectionString,
        string scenario,
        string resource,
        Task gate)
    {
        await gate;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)transaction;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            IF @result < 0 THROW 51010, 'Could not acquire test reservation lock.', 1;

            IF EXISTS (SELECT 1 FROM dbo.ReservationConcurrencyClaims WHERE Scenario = @scenario)
                SELECT CONVERT(bit, 0);
            ELSE
            BEGIN
                INSERT INTO dbo.ReservationConcurrencyClaims (Scenario, ClaimedOnUtc)
                VALUES (@scenario, SYSUTCDATETIME());
                SELECT CONVERT(bit, 1);
            END;
            """;
        command.Parameters.AddWithValue("@resource", resource);
        command.Parameters.AddWithValue("@scenario", scenario);
        var claimed = (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        if (claimed)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        return claimed;
    }
}
