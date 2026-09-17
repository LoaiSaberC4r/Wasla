using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class TicketQueueLock(WaslaDbContext dbContext) : ITicketQueueLock
{
    public Task AcquirePracticeDayAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
        => AcquireAsync($"wasla:ticket:queue:{doctorPracticeId:N}:{businessDate:yyyyMMdd}", cancellationToken);

    public Task AcquirePatientPracticeAsync(
        Guid patientId,
        Guid doctorPracticeId,
        CancellationToken cancellationToken = default)
        => AcquireAsync($"wasla:ticket:patient:{patientId:N}:{doctorPracticeId:N}", cancellationToken);

    public Task AcquireIdempotencyAsync(
        Guid actorApplicationUserId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => AcquireAsync($"wasla:ticket:idem:{Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{actorApplicationUserId:N}|{operation}|{idempotencyKey}")))}",
            cancellationToken);

    private async Task AcquireAsync(string resource, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return;
        }

        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Ticket locks require an active transaction.");
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            SELECT @result;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.DbType = DbType.String;
        parameter.Size = 255;
        parameter.Value = resource.Length <= 255 ? resource : resource[..255];
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new InvalidOperationException("Unable to acquire the ticket coordination lock.");
        }
    }
}

internal sealed class TicketNumberAllocator(WaslaDbContext dbContext) : ITicketNumberAllocator
{
    public async Task<int> AllocateNextAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            var existing = await dbContext.TicketDailyCounters.SingleOrDefaultAsync(
                item => item.DoctorPracticeId == doctorPracticeId &&
                        item.BusinessDate == businessDate,
                cancellationToken);
            if (existing is null)
            {
                existing = new TicketDailyCounter(Guid.NewGuid(), doctorPracticeId, businessDate, 1);
                await dbContext.TicketDailyCounters.AddAsync(existing, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                return 1;
            }

            var next = existing.AllocateNext();
            await dbContext.SaveChangesAsync(cancellationToken);
            return next;
        }

        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Ticket number allocation requires an active transaction.");
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            MERGE [TicketDailyCounters] WITH (HOLDLOCK) AS target
            USING (VALUES (@id, @practiceId, @businessDate))
                AS source ([Id], [DoctorPracticeId], [BusinessDate])
            ON target.[DoctorPracticeId] = source.[DoctorPracticeId]
               AND target.[BusinessDate] = source.[BusinessDate]
            WHEN MATCHED THEN
                UPDATE SET [LastNumber] = target.[LastNumber] + 1
            WHEN NOT MATCHED THEN
                INSERT ([Id], [DoctorPracticeId], [BusinessDate], [LastNumber])
                VALUES (source.[Id], source.[DoctorPracticeId], source.[BusinessDate], 1)
            OUTPUT inserted.[LastNumber];
            """;
        AddParameter(command, "@id", DbType.Guid, Guid.NewGuid());
        AddParameter(command, "@practiceId", DbType.Guid, doctorPracticeId);
        AddParameter(command, "@businessDate", DbType.Date, businessDate.ToDateTime(TimeOnly.MinValue));
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

internal sealed class ReservationNoShowRuntimeReader(WaslaDbContext dbContext)
    : IReservationNoShowRuntimeReader
{
    public Task<bool> HasSameDayNoShowAsync(
        Guid patientId,
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
        => dbContext.Reservations.AsNoTracking().AnyAsync(
            item => item.PatientId == patientId &&
                    item.DoctorPracticeId == doctorPracticeId &&
                    item.BusinessDate == businessDate &&
                    item.Status == ReservationStatus.NoShow,
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> FindEligibleReservationIdsAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        DateTime nowUtc,
        int checkInGracePeriodMinutes,
        int passedPatientsThreshold,
        CancellationToken cancellationToken = default)
    {
        if (passedPatientsThreshold < 1)
        {
            return [];
        }

        return await dbContext.Reservations.AsNoTracking()
            .Where(reservation =>
                reservation.DoctorPracticeId == doctorPracticeId &&
                reservation.BusinessDate == businessDate &&
                reservation.Status == ReservationStatus.Active &&
                nowUtc > reservation.ScheduledStartUtc.AddMinutes(checkInGracePeriodMinutes) &&
                dbContext.Tickets.Count(ticket =>
                    ticket.DoctorPracticeId == doctorPracticeId &&
                    ticket.BusinessDate == businessDate &&
                    ticket.InProgressOnUtc.HasValue &&
                    ticket.InProgressOnUtc.Value >
                        reservation.ScheduledStartUtc.AddMinutes(checkInGracePeriodMinutes)) >=
                    passedPatientsThreshold)
            .OrderBy(item => item.ScheduledStartUtc)
            .Select(item => item.Id)
            .Take(100)
            .ToArrayAsync(cancellationToken);
    }
}
