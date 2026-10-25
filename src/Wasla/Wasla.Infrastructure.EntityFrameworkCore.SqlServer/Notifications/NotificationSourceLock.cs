using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

internal static class NotificationSourceLock
{
    internal static string Resource(string sourceType, Guid sourceId) => $"Wasla:Notification:{sourceType}:{sourceId:N}";
    internal static async Task AcquireAsync(WaslaDbContext db, string resource, string owner, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return;
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner=@owner,@LockTimeout=15000; IF @r < 0 THROW 51017,'Notification lock unavailable.',1;";
        Add(command, "@resource", resource); Add(command, "@owner", owner);
        await command.ExecuteNonQueryAsync(ct);
    }
    internal static async Task<IAsyncDisposable> SessionAsync(WaslaDbContext db, string resource, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return new SessionLease(db, resource, false);
        var opened = db.Database.GetDbConnection().State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(ct);
        try { await AcquireAsync(db, resource, "Session", ct); }
        catch { if (opened) await db.Database.CloseConnectionAsync(); throw; }
        return new SessionLease(db, resource, opened);
    }
    private static void Add(System.Data.Common.DbCommand command, string name, object value)
    { var p = command.CreateParameter(); p.ParameterName = name; p.Value = value; command.Parameters.Add(p); }
    private sealed class SessionLease(WaslaDbContext db, string resource, bool close) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (!db.Database.IsSqlServer()) return;
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "EXEC sys.sp_releaseapplock @Resource=@resource,@LockOwner='Session';";
                Add(command, "@resource", resource); await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally { if (close) await db.Database.CloseConnectionAsync(); }
        }
    }
}
