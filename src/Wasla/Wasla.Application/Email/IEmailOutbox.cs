namespace Wasla.Application.Email;

public interface IEmailOutbox
{
    Task QueueAsync(QueueEmailMessage message, CancellationToken cancellationToken = default);
}
