using BuildingBlock.Application.Email;
using BuildingBlock.Infrastructure.Email;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Options;
using BuildingBlock.Infrastructure.Service;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BuildingBlock.Tests;

public sealed class Phase4EmailTests
{
    [Fact]
    public async Task Mailkit_sender_uses_configured_sender_and_all_recipient_groups()
    {
        var fakeClient = new FakeSmtpClient();
        var sender = CreateSender(fakeClient);

        await sender.SendAsync(new EmailMessage
        {
            To = new[] { "to@example.com" },
            Cc = new[] { "cc@example.com" },
            Bcc = new[] { "bcc@example.com" },
            ReplyTo = new[] { "reply@example.com" },
            Subject = "Hello",
            HtmlBody = "<b>Hello</b>",
            TextBody = "Hello"
        }, TestContext.Current.CancellationToken);

        var sent = fakeClient.SentMessage;
        Assert.NotNull(sent);
        var message = sent!;
        Assert.Equal("configured@example.com", message.From.Mailboxes.Single().Address);
        Assert.Equal("to@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("cc@example.com", message.Cc.Mailboxes.Single().Address);
        Assert.Equal("bcc@example.com", message.Bcc.Mailboxes.Single().Address);
        Assert.Equal("reply@example.com", message.ReplyTo.Mailboxes.Single().Address);
        Assert.True(fakeClient.Connected);
        Assert.True(fakeClient.Authenticated);
        Assert.Equal(SecureSocketOptions.StartTls, fakeClient.SecureSocketOptions);
    }

    [Fact]
    public void Email_message_no_longer_exposes_caller_controlled_from()
    {
        Assert.DoesNotContain(
            typeof(EmailMessage).GetProperties(),
            property => property.Name == "From");
        Assert.NotNull(typeof(IEmailTemplateRenderer).GetMethod(nameof(IEmailTemplateRenderer.RenderAsync)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Invalid_recipients_are_rejected(string recipient)
    {
        var sender = CreateSender(new FakeSmtpClient());

        var exception = await Assert.ThrowsAsync<EmailServiceException>(() =>
            sender.SendAsync(new EmailMessage
            {
                To = string.IsNullOrEmpty(recipient) ? Array.Empty<string>() : new[] { recipient },
                Subject = "Hello",
                TextBody = "Hello"
            }, TestContext.Current.CancellationToken));

        Assert.Equal(ExternalServiceErrorCodes.Email.InvalidMessage, exception.Error.Code);
    }

    [Fact]
    public async Task Attachment_limits_and_unsafe_names_are_rejected()
    {
        var sender = CreateSender(new FakeSmtpClient(), options =>
        {
            options.MaxAttachmentBytes = 4;
            options.MaxTotalAttachmentBytes = 8;
        });

        await Assert.ThrowsAsync<EmailServiceException>(() =>
            sender.SendAsync(new EmailMessage
            {
                To = new[] { "to@example.com" },
                Subject = "Hello",
                TextBody = "Hello",
                Attachments = new[]
                {
                    EmailAttachment.FromBytes("large.txt", new byte[] { 1, 2, 3, 4, 5 }, "text/plain")
                }
            }, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<EmailServiceException>(() =>
            sender.SendAsync(new EmailMessage
            {
                To = new[] { "to@example.com" },
                Subject = "Hello",
                TextBody = "Hello",
                Attachments = new[]
                {
                    EmailAttachment.FromBytes("../evil.txt", new byte[] { 1 }, "text/plain")
                }
            }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Attachment_streams_are_disposed_after_send_unless_left_open()
    {
        var stream = new TrackingStream(new byte[] { 1, 2, 3 });
        var fakeClient = new FakeSmtpClient();
        var sender = CreateSender(fakeClient);

        await sender.SendAsync(new EmailMessage
        {
            To = new[] { "to@example.com" },
            Subject = "Hello",
            TextBody = "Hello",
            Attachments = new[]
            {
                new EmailAttachment("file.txt", stream, "text/plain", length: 3)
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task Connection_failure_maps_to_safe_infrastructure_error()
    {
        var sender = CreateSender(new FakeSmtpClient { ThrowOnConnect = true });

        var exception = await Assert.ThrowsAsync<EmailServiceException>(() =>
            sender.SendAsync(new EmailMessage
            {
                To = new[] { "to@example.com" },
                Subject = "Hello",
                TextBody = "Hello"
            }, TestContext.Current.CancellationToken));

        Assert.Equal(ExternalServiceErrorCodes.Email.ConnectionFailed, exception.Error.Code);
    }

    private static MailKitEmailSender CreateSender(
        FakeSmtpClient client,
        Action<SmtpOptions>? configure = null)
    {
        var options = new SmtpOptions
        {
            Host = "localhost",
            Port = 25,
            UserName = "user",
            Password = "password",
            FromEmail = "configured@example.com",
            FromName = "Configured",
            RequireStartTls = true
        };
        configure?.Invoke(options);

        return new MailKitEmailSender(
            Options.Create(options),
            new FakeSmtpClientFactory(client),
            NullLogger<MailKitEmailSender>.Instance);
    }

    private sealed class FakeSmtpClientFactory : ISmtpClientFactory
    {
        private readonly FakeSmtpClient _client;

        public FakeSmtpClientFactory(FakeSmtpClient client)
        {
            _client = client;
        }

        public ISmtpClient Create() => _client;
    }

    private sealed class FakeSmtpClient : ISmtpClient
    {
        public int Timeout { get; set; }

        public bool CheckCertificateRevocation { get; set; }

        public bool Connected { get; private set; }

        public bool Authenticated { get; private set; }

        public bool ThrowOnConnect { get; init; }

        public SecureSocketOptions SecureSocketOptions { get; private set; }

        public MimeMessage? SentMessage { get; private set; }

        public Task ConnectAsync(string host, int port, SecureSocketOptions secureSocketOptions, CancellationToken ct)
        {
            if (ThrowOnConnect)
            {
                throw new IOException("provider detail");
            }

            Connected = true;
            SecureSocketOptions = secureSocketOptions;
            return Task.CompletedTask;
        }

        public Task AuthenticateAsync(string userName, string password, CancellationToken ct)
        {
            Authenticated = true;
            return Task.CompletedTask;
        }

        public Task SendAsync(MimeMessage message, CancellationToken ct)
        {
            SentMessage = message;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(bool quit, CancellationToken ct)
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    private sealed class TrackingStream : MemoryStream
    {
        public TrackingStream(byte[] buffer)
            : base(buffer)
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
