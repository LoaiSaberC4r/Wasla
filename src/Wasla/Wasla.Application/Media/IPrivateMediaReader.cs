namespace Wasla.Application.Media;

public sealed record PrivateMedia(Stream Content, string ContentType, string FileName);

public interface IPrivateMediaReader
{
    Task<PrivateMedia?> OpenAsync(string key, CancellationToken cancellationToken = default);
}
