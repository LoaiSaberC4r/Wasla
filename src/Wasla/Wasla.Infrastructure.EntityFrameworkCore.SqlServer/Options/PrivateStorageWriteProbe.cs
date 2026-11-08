namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

public static class PrivateStorageWriteProbe
{
    public static void Verify(string? directory)
    {
        const string error = "Private storage must be provisioned and writable, including temporary file cleanup.";
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException(error);

        var probe = Path.Combine(directory, ".wasla-write-probe-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            try
            {
                using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                created = true;
                stream.Write("Wasla storage readiness"u8);
                stream.Flush(flushToDisk: true);
            }
            finally
            {
                if (created) File.Delete(probe);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            // Do not retain filesystem exception details or physical paths in activation/health errors.
            throw new InvalidOperationException(error);
        }
    }
}
