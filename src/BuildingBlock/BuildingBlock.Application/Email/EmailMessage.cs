namespace BuildingBlock.Application.Email
{
    public sealed class EmailMessage
    {
        public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Bcc { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ReplyTo { get; init; } = Array.Empty<string>();

        public string Subject { get; init; } = string.Empty;

        public string? HtmlBody { get; init; }

        public string? TextBody { get; init; }

        public IReadOnlyList<EmailAttachment> Attachments { get; init; } = Array.Empty<EmailAttachment>();
    }

    public sealed class EmailAttachment : IAsyncDisposable, IDisposable
    {
        public EmailAttachment(
            string fileName,
            Stream content,
            string contentType,
            long? length = null,
            bool leaveOpen = false)
        {
            FileName = fileName;
            Content = content;
            ContentType = contentType;
            Length = length;
            LeaveOpen = leaveOpen;
        }

        public string FileName { get; }

        public Stream Content { get; }

        public string ContentType { get; }

        public long? Length { get; }

        public bool LeaveOpen { get; }

        public static EmailAttachment FromBytes(
            string fileName,
            byte[] content,
            string contentType)
        {
            ArgumentNullException.ThrowIfNull(content);
            return new EmailAttachment(
                fileName,
                new MemoryStream(content, writable: false),
                contentType,
                content.LongLength);
        }

        public void Dispose()
        {
            if (!LeaveOpen)
            {
                Content.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!LeaveOpen)
            {
                await Content.DisposeAsync();
            }
        }
    }
}
