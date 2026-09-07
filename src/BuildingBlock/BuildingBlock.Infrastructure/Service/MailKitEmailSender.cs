using BuildingBlock.Application.Diagnostics;
using BuildingBlock.Application.Email;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Email;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Options;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net.Sockets;

namespace BuildingBlock.Infrastructure.Service
{
    internal sealed class MailKitEmailSender : IEmailSender
    {
        private static readonly Action<ILogger, int, int, Exception?> EmailSent =
            LoggerMessage.Define<int, int>(
                LogLevel.Information,
                new EventId(2300, nameof(EmailSent)),
                "Email sent. RecipientCount={RecipientCount} AttachmentCount={AttachmentCount}");

        private readonly SmtpOptions _options;
        private readonly ISmtpClientFactory _smtpClientFactory;
        private readonly ILogger<MailKitEmailSender> _logger;

        public MailKitEmailSender(
            IOptions<SmtpOptions> options,
            ISmtpClientFactory smtpClientFactory,
            ILogger<MailKitEmailSender> logger)
        {
            _options = options.Value;
            _smtpClientFactory = smtpClientFactory;
            _logger = logger;
        }

        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            ct.ThrowIfCancellationRequested();

            try
            {
                ValidateMessage(message);
                var mime = BuildMimeMessage(message);

                await using var client = _smtpClientFactory.Create();
                client.Timeout = _options.TimeoutMilliseconds;
                client.CheckCertificateRevocation = true;

                var secureSocket = _options.UseSsl
                    ? SecureSocketOptions.SslOnConnect
                    : _options.RequireStartTls
                        ? SecureSocketOptions.StartTls
                        : SecureSocketOptions.StartTlsWhenAvailable;

                await client.ConnectAsync(_options.Host, _options.Port, secureSocket, ct);
                if (!string.IsNullOrWhiteSpace(_options.UserName))
                {
                    await client.AuthenticateAsync(_options.UserName, _options.Password, ct);
                }

                await client.SendAsync(mime, ct);
                await client.DisconnectAsync(true, ct);

                BuildingBlockDiagnostics.RecordEmailSent();
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    EmailSent(_logger, CountRecipients(message), message.Attachments.Count, null);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                BuildingBlockDiagnostics.RecordEmailFailure("Cancelled");
                throw;
            }
            catch (EmailServiceException exception)
            {
                BuildingBlockDiagnostics.RecordEmailFailure(exception.Error.Type.ToString());
                throw;
            }
            catch (AuthenticationException exception)
            {
                BuildingBlockDiagnostics.RecordEmailFailure("Authentication");
                throw Failure(
                    ExternalServiceErrorCodes.Email.AuthenticationFailed,
                    "SMTP authentication failed.",
                    exception);
            }
            catch (TimeoutException exception)
            {
                BuildingBlockDiagnostics.RecordEmailFailure("Timeout");
                throw Failure(
                    ExternalServiceErrorCodes.Email.Timeout,
                    "SMTP operation timed out.",
                    exception);
            }
            catch (SmtpCommandException exception)
            {
                BuildingBlockDiagnostics.RecordEmailFailure("Send");
                throw Failure(
                    ExternalServiceErrorCodes.Email.SendFailed,
                    "SMTP server rejected the message.",
                    exception);
            }
            catch (SmtpProtocolException exception)
            {
                BuildingBlockDiagnostics.RecordEmailFailure("Send");
                throw Failure(
                    ExternalServiceErrorCodes.Email.SendFailed,
                    "SMTP protocol failure occurred.",
                    exception);
            }
            catch (SocketException exception)
            {
                BuildingBlockDiagnostics.RecordEmailFailure("Connection");
                throw Failure(
                    ExternalServiceErrorCodes.Email.ConnectionFailed,
                    "SMTP connection failed.",
                    exception);
            }
            catch (IOException exception)
            {
                BuildingBlockDiagnostics.RecordEmailFailure("Connection");
                throw Failure(
                    ExternalServiceErrorCodes.Email.ConnectionFailed,
                    "SMTP connection failed.",
                    exception);
            }
            finally
            {
                foreach (var attachment in message.Attachments)
                {
                    await attachment.DisposeAsync();
                }
            }
        }

        private MimeMessage BuildMimeMessage(EmailMessage message)
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
            AddRecipients(mime.To, message.To);
            AddRecipients(mime.Cc, message.Cc);
            AddRecipients(mime.Bcc, message.Bcc);
            AddRecipients(mime.ReplyTo, message.ReplyTo);
            mime.Subject = message.Subject.Trim();

            var body = new BodyBuilder
            {
                HtmlBody = string.IsNullOrWhiteSpace(message.HtmlBody) ? null : message.HtmlBody,
                TextBody = string.IsNullOrWhiteSpace(message.TextBody) ? null : message.TextBody
            };

            foreach (var attachment in message.Attachments)
            {
                var safeName = Path.GetFileName(attachment.FileName);
                if (attachment.Content.CanSeek)
                {
                    attachment.Content.Position = 0;
                }

                body.Attachments.Add(safeName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }

            mime.Body = body.ToMessageBody();
            return mime;
        }

        private void ValidateMessage(EmailMessage message)
        {
            if (message.To.Count == 0)
            {
                throw InvalidMessage("At least one email recipient is required.");
            }

            var recipientCount = CountRecipients(message);
            if (recipientCount > _options.MaxRecipients)
            {
                throw InvalidMessage("Email recipient count exceeds the configured limit.");
            }

            foreach (var recipient in message.To.Concat(message.Cc).Concat(message.Bcc).Concat(message.ReplyTo))
            {
                _ = ParseMailbox(recipient);
            }

            if (string.IsNullOrWhiteSpace(message.Subject))
            {
                throw InvalidMessage("Email subject is required.");
            }

            if (string.IsNullOrWhiteSpace(message.HtmlBody) &&
                string.IsNullOrWhiteSpace(message.TextBody))
            {
                throw InvalidMessage("Email body is required.");
            }

            if (message.Attachments.Count > _options.MaxAttachments)
            {
                throw InvalidMessage("Email attachment count exceeds the configured limit.");
            }

            long totalAttachmentBytes = 0;
            foreach (var attachment in message.Attachments)
            {
                ValidateAttachmentName(attachment.FileName);

                var length = attachment.Length ??
                    (attachment.Content.CanSeek ? attachment.Content.Length : (long?)null);
                if (length is null)
                {
                    throw InvalidMessage("Non-seekable email attachments must declare a length.");
                }

                if (length <= 0 || length > _options.MaxAttachmentBytes)
                {
                    throw InvalidMessage("Email attachment size is invalid.");
                }

                totalAttachmentBytes += length.Value;
                if (totalAttachmentBytes > _options.MaxTotalAttachmentBytes)
                {
                    throw InvalidMessage("Total email attachment size exceeds the configured limit.");
                }

                try
                {
                    _ = ContentType.Parse(attachment.ContentType);
                }
                catch (Exception exception)
                {
                    throw InvalidMessage("Email attachment content type is invalid.", exception);
                }
            }
        }

        private static void AddRecipients(InternetAddressList list, IEnumerable<string> recipients)
        {
            foreach (var recipient in recipients)
            {
                list.Add(ParseMailbox(recipient));
            }
        }

        private static MailboxAddress ParseMailbox(string value)
        {
            try
            {
                var mailbox = MailboxAddress.Parse(value);
                if (string.IsNullOrWhiteSpace(mailbox.Address) ||
                    !mailbox.Address.Contains('@', StringComparison.Ordinal) ||
                    mailbox.Address.EndsWith('@') ||
                    mailbox.Address.StartsWith('@'))
                {
                    throw new FormatException("Email address must include a local part and domain.");
                }

                return mailbox;
            }
            catch (Exception exception)
            {
                throw InvalidMessage("Email address is invalid.", exception);
            }
        }

        private static int CountRecipients(EmailMessage message)
            => message.To.Count + message.Cc.Count + message.Bcc.Count;

        private static void ValidateAttachmentName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                Path.IsPathRooted(fileName) ||
                fileName.Contains(':', StringComparison.Ordinal) ||
                fileName.Contains('/', StringComparison.Ordinal) ||
                fileName.Contains('\\', StringComparison.Ordinal) ||
                !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw InvalidMessage("Email attachment file name is invalid.");
            }
        }

        private static EmailServiceException InvalidMessage(string message, Exception? innerException = null)
            => new(
                Error.Validation(
                    ExternalServiceErrorCodes.Email.InvalidMessage,
                    message,
                    source: "Email"),
                innerException);

        private static EmailServiceException Failure(string code, string message, Exception innerException)
            => new(
                Error.Infra(
                    code,
                    message,
                    source: "Email"),
                innerException);
    }
}
