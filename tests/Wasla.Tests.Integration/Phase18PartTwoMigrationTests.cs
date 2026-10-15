#pragma warning disable EF1002 // Database/table/column names originate from generated test IDs and sys metadata.
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Archives;
using Wasla.Domain.Payments;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Microsoft.Extensions.Options;

namespace Wasla.Tests.Integration;

public sealed class Phase18PartTwoMigrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Nonempty_baseline_expand_population_cutover_restart_backfill_and_query_plan_rehearsal()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var app=await Phase13ApiFixture.CreateAsync(true);
        await app.WithDbAsync(async source=>
        {
            for(var index=1;index<=1000;index++)
            {
                var at=app.Clock.UtcNow.AddDays(-30);var date=app.Today.AddDays(-30);
                var ticket=Ticket.CreateWalkIn(new(Guid.NewGuid(),app.DoctorId,app.PracticeId,app.PatientId,null,date,index,TicketSource.WalkIn,
                    app.SegmentId,"Synthetic","Synthetic",0,app.ConsultationTypeId,"NewConsultation","Synthetic","Synthetic",300,"Africa/Cairo",at,at,CheckInMode.WalkIn,app.DoctorUserId,null)).Value;
                ticket.Cancel(app.DoctorUserId,"Synthetic cancelled history",at.AddMinutes(1));source.Tickets.Add(ticket);
                source.Payments.Add(Payment.RecordPaid(new(Guid.NewGuid(),app.DoctorId,app.PracticeId,app.PatientId,null,ticket.Id,300,300,
                    "PAY-SYNTHETIC-"+index,index,date,PaymentMethod.Cash,null,null,app.DoctorUserId,at)).Value);
            }
            await source.SaveChangesAsync(ct);
        });
        string? sourceConnection=null;await app.WithDbAsync(db=>{sourceConnection=db.Database.GetConnectionString();return Task.CompletedTask;});
        var builder=new SqlConnectionStringBuilder(sourceConnection!){InitialCatalog="WaslaPhase18PartTwoMigration_"+Guid.NewGuid().ToString("N")};
        var sourceName=new SqlConnectionStringBuilder(sourceConnection!).InitialCatalog;var targetName=builder.InitialCatalog;
        await using var db=new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(builder.ConnectionString).Options,app.Clock);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261014095416_Phase18ExportScopeSnapshot",ct);
            await CopyNonemptyBaseline(sourceName,db,ct);
            await db.GetService<IMigrator>().MigrateAsync("20261014170532_Phase18PartTwoArchiveExpansion",ct);
            var failure=await Assert.ThrowsAsync<SqlException>(()=>db.Database.MigrateAsync(ct));Assert.Equal(51000,failure.Number);
            Assert.Equal(1000,await Scalar(db,"SELECT COUNT_BIG(*) FROM dbo.Tickets",ct));
            var population=new ArchiveRegistryPopulation(db);var batches=0;var total=0;
            while(true){var count=await population.PopulateAsync(100,ct);Assert.InRange(count,0,200);if(count==0)break;total+=count;batches++;}
            Assert.Equal(1000,total);Assert.Equal(10,batches);Assert.Equal(0,await population.PopulateAsync(100,ct));
            await db.Database.MigrateAsync(ct);await db.Database.MigrateAsync(ct);Assert.False((await db.Database.GetPendingMigrationsAsync(ct)).Any());
            Assert.Equal(1000,await db.TicketRegistry.LongCountAsync(ct));
            var beforeIds=await db.TicketHistories.Select(x=>x.Id).ToArrayAsync(ct);var beforePaid=await db.Payments.SumAsync(x=>x.Amount,ct);
            var timing=Stopwatch.StartNew();long moved=0;var cycles=0;
            // Fresh DbContext per cycle models process restarts and proves durable progress.
            while(moved<1000)
            {
                await using var cycleDb=new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(builder.ConnectionString).Options,app.Clock);
                var engine=new ArchiveEngine(cycleDb,new SqlQueueLock(cycleDb),app.Clock,Options.Create(new ArchiveOptions{Enabled=true,ReadModelsVerified=true,BatchSize=50}));
                var run=await engine.ProcessAsync(ct);Assert.NotNull(run);Assert.InRange(run.Scanned,0,50);moved+=run.ArchivedTickets;cycles++;Assert.True(cycles<=22);
            }
            output.WriteLine($"1000 relocated in {cycles} bounded/restarted cycles, {timing.Elapsed.TotalSeconds:F2}s; no SLA asserted.");
            Assert.Equal(0,await db.Tickets.LongCountAsync(ct));Assert.Equal(1000,await db.Set<ArchivedTicket>().LongCountAsync(ct));
            Assert.Equal(beforePaid,await db.Payments.SumAsync(x=>x.Amount,ct));Assert.Equal(beforeIds.Order(),(await db.Set<ArchivedTicketHistory>().Select(x=>x.Id).ToArrayAsync(ct)).Order());
            Assert.Equal(1000,await db.AllTickets.LongCountAsync(ct));Assert.Equal(beforeIds.Length,await db.AllTicketHistories.LongCountAsync(ct));
            await db.Database.OpenConnectionAsync(ct);using var plan=db.Database.GetDbConnection().CreateCommand();
            plan.CommandText="SET SHOWPLAN_XML ON";await plan.ExecuteNonQueryAsync(ct);
            try
            {
                plan.CommandText=db.Set<ArchivedTicket>().Where(x=>x.DoctorPracticeId==app.PracticeId).OrderByDescending(x=>x.BusinessDate).ThenByDescending(x=>x.Id).Take(21).Select(x=>x.Id).ToQueryString();
                var xml=(string)(await plan.ExecuteScalarAsync(ct))!;Assert.Contains("ShowPlanXML",xml);Assert.Contains("DoctorPracticeId_BusinessDate_Id",xml);output.WriteLine("Archive practice/date/ID index appears in the SQL plan.");
                var evidence=Environment.GetEnvironmentVariable("WASLA_PART2_EVIDENCE_DIRECTORY");
                if(evidence!=null){Directory.CreateDirectory(evidence);await File.WriteAllTextAsync(Path.Combine(evidence,"archive-page-plan.xml"),xml,ct);await File.WriteAllTextAsync(Path.Combine(evidence,"backfill-performance.txt"),$"Records=1000; batchSize=50; restartedCycles={cycles}; durationSeconds={timing.Elapsed.TotalSeconds:F2}; no SLA asserted",ct);}
            }
            finally {plan.Parameters.Clear();plan.CommandText="SET SHOWPLAN_XML OFF";await plan.ExecuteNonQueryAsync(ct);}
            await BackupRestore(builder.ConnectionString,db,ct);
        }
        finally
        {
            // Only the exact newly allocated synthetic test database is removed.
            Assert.StartsWith("WaslaPhase18PartTwoMigration_",targetName);await db.Database.EnsureDeletedAsync(ct);
        }
    }
    private static async Task<long> Scalar(WaslaDbContext db,string sql,CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText=sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct),System.Globalization.CultureInfo.InvariantCulture);
    }
    internal static async Task CopyNonemptyBaseline(string source,WaslaDbContext target,CancellationToken ct)
    {
        await target.Database.OpenConnectionAsync(ct);var connection=target.Database.GetDbConnection();using var command=connection.CreateCommand();
        command.CommandText="SELECT t.name FROM sys.tables t WHERE SCHEMA_NAME(t.schema_id)='dbo' AND t.name<>'__EFMigrationsHistory'";
        var tables=new List<string>();await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))tables.Add(reader.GetString(0));
        var active=new HashSet<string>();foreach(var table in tables){command.CommandText=$"SELECT COUNT_BIG(*) FROM [{source}].dbo.[{table}]";if(Convert.ToInt64(await command.ExecuteScalarAsync(ct),System.Globalization.CultureInfo.InvariantCulture)>0)active.Add(table);}
        command.CommandText="SELECT OBJECT_NAME(parent_object_id),OBJECT_NAME(referenced_object_id) FROM sys.foreign_keys";
        var edges=new List<(string Child,string Parent)>();await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))edges.Add((reader.GetString(0),reader.GetString(1)));
        var copied=new HashSet<string>();while(active.Count>copied.Count)
        {
            var next=active.Where(t=>!copied.Contains(t)&&!edges.Any(e=>e.Child==t&&e.Parent!=t&&active.Contains(e.Parent)&&!copied.Contains(e.Parent))).ToArray();Assert.NotEmpty(next);
            foreach(var table in next)
            {
                command.CommandText=$"SELECT name,is_identity FROM sys.columns WHERE object_id=OBJECT_ID('dbo.{table}') AND is_computed=0 AND system_type_id<>189 ORDER BY column_id";
                var columns=new List<string>();var identity=false;await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct)){columns.Add("["+reader.GetString(0)+"]");identity|=reader.GetBoolean(1);}
                command.CommandText=$"SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.is_primary_key=1 AND i.object_id=OBJECT_ID('dbo.{table}') ORDER BY ic.key_ordinal";
                var keys=new List<string>();await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))keys.Add(reader.GetString(0));Assert.NotEmpty(keys);
                var fields=string.Join(',',columns);var selected=string.Join(',',columns.Select(c=>"source."+c));var matches=string.Join(" AND ",keys.Select(k=>$"existing.[{k}]=source.[{k}]"));
                command.CommandText=(identity?$"SET IDENTITY_INSERT dbo.[{table}] ON;":"")+$"INSERT dbo.[{table}] ({fields}) SELECT {selected} FROM [{source}].dbo.[{table}] source WHERE NOT EXISTS (SELECT 1 FROM dbo.[{table}] existing WHERE {matches});"+(identity?$"SET IDENTITY_INSERT dbo.[{table}] OFF;":"");await command.ExecuteNonQueryAsync(ct);copied.Add(table);
            }
        }
    }
    private async Task BackupRestore(string connectionString,WaslaDbContext db,CancellationToken ct)
    {
        var database=new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        var restore="WaslaPhase18PartTwoRestore_"+Guid.NewGuid().ToString("N");
        var masterBuilder=new SqlConnectionStringBuilder(connectionString){InitialCatalog="master"};
        await using var connection=new SqlConnection(masterBuilder.ConnectionString);await connection.OpenAsync(ct);using var command=connection.CreateCommand();command.CommandTimeout=120;
        command.CommandText="SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')),CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')),CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))";
        string backupDirectory,dataDirectory,logDirectory;await using(var reader=await command.ExecuteReaderAsync(ct)){Assert.True(await reader.ReadAsync(ct));backupDirectory=reader.GetString(0);dataDirectory=reader.GetString(1);logDirectory=reader.GetString(2);}
        var file=Path.Combine(backupDirectory,database+"_"+Guid.NewGuid().ToString("N")+".bak");
        command.CommandText=$"BACKUP DATABASE [{database}] TO DISK=@file WITH COPY_ONLY,CHECKSUM";command.Parameters.AddWithValue("@file",file);await command.ExecuteNonQueryAsync(ct);
        command.CommandText="RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM";await command.ExecuteNonQueryAsync(ct);command.Parameters.Clear();
        await db.Database.OpenConnectionAsync(ct);using var files=db.Database.GetDbConnection().CreateCommand();files.CommandText="SELECT name,type FROM sys.database_files";
        var moves=new List<string>();await using(var reader=await files.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))
        {
            var logical=reader.GetString(0).Replace("'","''",StringComparison.Ordinal);var log=reader.GetByte(1)==1;
            var path=Path.Combine(log?logDirectory:dataDirectory,restore+(log?"_log.ldf":".mdf")).Replace("'","''",StringComparison.Ordinal);
            moves.Add($"MOVE N'{logical}' TO N'{path}'");
        }
        command.CommandText=$"RESTORE DATABASE [{restore}] FROM DISK=@file WITH {string.Join(',',moves)},CHECKSUM";command.Parameters.AddWithValue("@file",file);await command.ExecuteNonQueryAsync(ct);
        var restoreBuilder=new SqlConnectionStringBuilder(connectionString){InitialCatalog=restore};await using var restored=new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(restoreBuilder.ConnectionString).Options);
        try{Assert.Equal(1000,await restored.Set<ArchivedTicket>().LongCountAsync(ct));Assert.Equal(300000,await restored.Payments.SumAsync(x=>x.Amount,ct));output.WriteLine("Synthetic database COPY_ONLY/CHECKSUM backup, VERIFYONLY and separate-database restore passed.");}
        finally{Assert.StartsWith("WaslaPhase18PartTwoRestore_",restore);await restored.Database.EnsureDeletedAsync(ct);}
    }
    private sealed class SqlQueueLock(WaslaDbContext db):ITicketQueueLock
    {
        private Task<int> Acquire(string key,CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result=sp_getapplock @Resource={key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; IF @result<0 THROW 51000,'Synthetic queue lock',1;",ct);
        public async Task AcquirePatientPracticeAsync(Guid patientId,Guid practiceId,CancellationToken cancellationToken=default)=>await Acquire($"wasla:ticket:patient:{patientId:N}:{practiceId:N}",cancellationToken);
        public async Task AcquirePracticeDayAsync(Guid practiceId,DateOnly date,CancellationToken cancellationToken=default)=>await Acquire($"wasla:ticket:queue:{practiceId:N}:{date:yyyyMMdd}",cancellationToken);
        public async Task AcquireIdempotencyAsync(Guid actorId,string operation,string key,CancellationToken cancellationToken=default)=>await Acquire($"Synthetic:{actorId:N}:{operation}:{key}",cancellationToken);
    }
}
