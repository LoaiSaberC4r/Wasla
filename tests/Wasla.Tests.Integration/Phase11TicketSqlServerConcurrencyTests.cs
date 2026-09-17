using Microsoft.Data.SqlClient;

namespace Wasla.Tests.Integration;

public sealed class Phase11TicketSqlServerConcurrencyTests
{
    public static TheoryData<string, string> Phase11ContentionScenarios => new()
    {
        { "same-reservation-check-in", "wasla:ticket:queue:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:20260917" },
        { "duplicate-open-ticket", "wasla:ticket:patient:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" },
        { "call-next", "wasla:ticket:queue:cccccccccccccccccccccccccccccccc:20260917" },
        { "start-visit", "wasla:ticket:queue:dddddddddddddddddddddddddddddddd:20260917" },
        { "restore-no-show", "wasla:ticket:queue:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee:20260917" },
        { "idempotency", "wasla:ticket:idem:ffffffffffffffffffffffffffffffff:CallNext:PHASE11" }
    };

    [Theory]
    [MemberData(nameof(Phase11ContentionScenarios))]
    [Trait("Category", "SQLServerConcurrency")]
    public async Task Phase11_transaction_lock_allows_one_business_claim(
        string scenario,
        string resource)
    {
        var connectionString = Environment.GetEnvironmentVariable("WASLA_SQLSERVER_CONNECTION_STRING");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString),
            "WASLA_SQLSERVER_CONNECTION_STRING is required for SQL Server concurrency tests.");

        await PrepareClaimsAsync(connectionString!, scenario);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = TryClaimAsync(connectionString!, scenario, resource, gate.Task);
        var second = TryClaimAsync(connectionString!, scenario, resource, gate.Task);
        gate.SetResult();
        var outcomes = await Task.WhenAll(first, second);

        Assert.Equal(1, outcomes.Count(outcome => outcome));
        Assert.Equal(1, outcomes.Count(outcome => !outcome));
    }

    [Fact]
    [Trait("Category", "SQLServerConcurrency")]
    public async Task Phase11_atomic_daily_allocator_never_returns_duplicate_numbers()
    {
        var connectionString = Environment.GetEnvironmentVariable("WASLA_SQLSERVER_CONNECTION_STRING");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString),
            "WASLA_SQLSERVER_CONNECTION_STRING is required for SQL Server concurrency tests.");

        var practiceId = Guid.NewGuid();
        var businessDate = new DateOnly(2026, 9, 17);
        await PrepareCounterAsync(connectionString!, practiceId, businessDate);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = AllocateAsync(connectionString!, practiceId, businessDate, gate.Task);
        var second = AllocateAsync(connectionString!, practiceId, businessDate, gate.Task);
        gate.SetResult();
        var numbers = await Task.WhenAll(first, second);

        Assert.Equal([1, 2], numbers.Order().ToArray());
    }

    private static async Task PrepareClaimsAsync(string connectionString, string scenario)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID('dbo.Phase11TicketConcurrencyClaims', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Phase11TicketConcurrencyClaims
                (
                    Scenario nvarchar(100) NOT NULL PRIMARY KEY,
                    ClaimedOnUtc datetime2(3) NOT NULL
                );
            END;
            DELETE FROM dbo.Phase11TicketConcurrencyClaims WHERE Scenario = @scenario;
            """;
        command.Parameters.AddWithValue("@scenario", scenario);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
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
            IF @result < 0 THROW 51011, 'Could not acquire Phase 11 test lock.', 1;

            IF EXISTS (SELECT 1 FROM dbo.Phase11TicketConcurrencyClaims WHERE Scenario = @scenario)
                SELECT CONVERT(bit, 0);
            ELSE
            BEGIN
                INSERT INTO dbo.Phase11TicketConcurrencyClaims (Scenario, ClaimedOnUtc)
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

    private static async Task PrepareCounterAsync(
        string connectionString,
        Guid practiceId,
        DateOnly businessDate)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID('dbo.Phase11TicketDailyCounterClaims', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Phase11TicketDailyCounterClaims
                (
                    DoctorPracticeId uniqueidentifier NOT NULL,
                    BusinessDate date NOT NULL,
                    LastNumber int NOT NULL,
                    CONSTRAINT PK_Phase11TicketDailyCounterClaims
                        PRIMARY KEY (DoctorPracticeId, BusinessDate)
                );
            END;
            DELETE FROM dbo.Phase11TicketDailyCounterClaims
            WHERE DoctorPracticeId = @practiceId AND BusinessDate = @businessDate;
            """;
        command.Parameters.AddWithValue("@practiceId", practiceId);
        command.Parameters.AddWithValue("@businessDate", businessDate.ToDateTime(TimeOnly.MinValue));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> AllocateAsync(
        string connectionString,
        Guid practiceId,
        DateOnly businessDate,
        Task gate)
    {
        await gate;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)transaction;
        command.CommandText = """
            MERGE dbo.Phase11TicketDailyCounterClaims WITH (HOLDLOCK) AS target
            USING (VALUES (@practiceId, @businessDate)) AS source (DoctorPracticeId, BusinessDate)
            ON target.DoctorPracticeId = source.DoctorPracticeId
               AND target.BusinessDate = source.BusinessDate
            WHEN MATCHED THEN UPDATE SET LastNumber = target.LastNumber + 1
            WHEN NOT MATCHED THEN
                INSERT (DoctorPracticeId, BusinessDate, LastNumber)
                VALUES (source.DoctorPracticeId, source.BusinessDate, 1)
            OUTPUT inserted.LastNumber;
            """;
        command.Parameters.AddWithValue("@practiceId", practiceId);
        command.Parameters.AddWithValue("@businessDate", businessDate.ToDateTime(TimeOnly.MinValue));
        var number = (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        return number;
    }
}
