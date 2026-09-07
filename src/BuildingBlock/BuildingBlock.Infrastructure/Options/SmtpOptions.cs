namespace BuildingBlock.Infrastructure.Options
{
    public sealed class SmtpOptions
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        public bool UseSsl { get; set; }

        public bool RequireStartTls { get; set; } = true;

        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string FromEmail { get; set; } = string.Empty;

        public string FromName { get; set; } = "Application";

        public long MaxAttachmentBytes { get; set; } = 10 * 1024 * 1024;

        public long MaxTotalAttachmentBytes { get; set; } = 25 * 1024 * 1024;

        public int MaxRecipients { get; set; } = 100;

        public int MaxAttachments { get; set; } = 20;

        public int TimeoutMilliseconds { get; set; } = 100_000;
    }
}
