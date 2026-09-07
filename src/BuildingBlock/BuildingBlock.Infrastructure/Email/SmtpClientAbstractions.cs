using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BuildingBlock.Infrastructure.Email
{
    internal interface ISmtpClientFactory
    {
        ISmtpClient Create();
    }

    internal interface ISmtpClient : IAsyncDisposable
    {
        int Timeout { get; set; }

        bool CheckCertificateRevocation { get; set; }

        Task ConnectAsync(
            string host,
            int port,
            SecureSocketOptions secureSocketOptions,
            CancellationToken ct);

        Task AuthenticateAsync(string userName, string password, CancellationToken ct);

        Task SendAsync(MimeMessage message, CancellationToken ct);

        Task DisconnectAsync(bool quit, CancellationToken ct);
    }

    internal sealed class MailKitSmtpClientFactory : ISmtpClientFactory
    {
        public ISmtpClient Create() => new MailKitSmtpClientAdapter(new SmtpClient());
    }

    internal sealed class MailKitSmtpClientAdapter : ISmtpClient
    {
        private readonly SmtpClient _client;

        public MailKitSmtpClientAdapter(SmtpClient client)
        {
            _client = client;
        }

        public int Timeout
        {
            get => _client.Timeout;
            set => _client.Timeout = value;
        }

        public bool CheckCertificateRevocation
        {
            get => _client.CheckCertificateRevocation;
            set => _client.CheckCertificateRevocation = value;
        }

        public Task ConnectAsync(
            string host,
            int port,
            SecureSocketOptions secureSocketOptions,
            CancellationToken ct)
            => _client.ConnectAsync(host, port, secureSocketOptions, ct);

        public Task AuthenticateAsync(string userName, string password, CancellationToken ct)
            => _client.AuthenticateAsync(userName, password, ct);

        public async Task SendAsync(MimeMessage message, CancellationToken ct)
            => _ = await _client.SendAsync(message, ct);

        public Task DisconnectAsync(bool quit, CancellationToken ct)
            => _client.DisconnectAsync(quit, ct);

        public ValueTask DisposeAsync()
        {
            _client.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
