using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wasla.Application.Features.Finance.Common;
using Wasla.Domain.Payments;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FinancialNumberAllocator(WaslaDbContext dbContext) : IFinancialNumberAllocator
{
    public async Task<FinancialTransactionNumber> AllocateNextAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        FinancialTransactionType transactionType,
        CancellationToken cancellationToken = default)
    {
        if (doctorPracticeId == Guid.Empty || businessDate == default ||
            transactionType is not FinancialTransactionType.Payment and not FinancialTransactionType.Refund)
        {
            throw new ArgumentException("Financial number scope is invalid.");
        }

        int sequence;
        if (!dbContext.Database.IsSqlServer())
        {
            var existing = await dbContext.FinancialDailyCounters.SingleOrDefaultAsync(
                item => item.DoctorPracticeId == doctorPracticeId &&
                        item.BusinessDate == businessDate &&
                        item.TransactionType == transactionType,
                cancellationToken);
            if (existing is null)
            {
                existing = new FinancialDailyCounter(
                    Guid.NewGuid(), doctorPracticeId, businessDate, transactionType, 1);
                await dbContext.FinancialDailyCounters.AddAsync(existing, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                sequence = 1;
            }
            else
            {
                sequence = existing.AllocateNext();
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            var transaction = dbContext.Database.CurrentTransaction
                ?? throw new InvalidOperationException(
                    "Financial number allocation requires an active transaction.");
            var connection = dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
                MERGE [FinancialDailyCounters] WITH (HOLDLOCK) AS target
                USING (VALUES (@id, @practiceId, @businessDate, @transactionType))
                    AS source ([Id], [DoctorPracticeId], [BusinessDate], [TransactionType])
                ON target.[DoctorPracticeId] = source.[DoctorPracticeId]
                   AND target.[BusinessDate] = source.[BusinessDate]
                   AND target.[TransactionType] = source.[TransactionType]
                WHEN MATCHED THEN
                    UPDATE SET [LastNumber] = target.[LastNumber] + 1
                WHEN NOT MATCHED THEN
                    INSERT ([Id], [DoctorPracticeId], [BusinessDate], [TransactionType], [LastNumber])
                    VALUES (source.[Id], source.[DoctorPracticeId], source.[BusinessDate],
                            source.[TransactionType], 1)
                OUTPUT inserted.[LastNumber];
                """;
            AddParameter(command, "@id", DbType.Guid, Guid.NewGuid());
            AddParameter(command, "@practiceId", DbType.Guid, doctorPracticeId);
            AddParameter(command, "@businessDate", DbType.Date, businessDate.ToDateTime(TimeOnly.MinValue));
            AddParameter(command, "@transactionType", DbType.Int32, (int)transactionType);
            sequence = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture);
        }

        var prefix = transactionType == FinancialTransactionType.Payment ? "PAY" : "REF";
        var transactionNumber = string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}-{doctorPracticeId:N}-{businessDate:yyyyMMdd}-{sequence:D6}");
        return new FinancialTransactionNumber(sequence, transactionNumber);
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
