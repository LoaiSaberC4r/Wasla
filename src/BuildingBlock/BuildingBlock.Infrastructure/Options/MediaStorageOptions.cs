namespace BuildingBlock.Infrastructure.Options
{
    public sealed class MediaStorageOptions
    {
        public string RootPath { get; set; } = Path.Combine("App_Data", "Media");

        public string? ContentRootPath { get; set; }

        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

        public bool RequireMalwareScanner { get; set; }

        public string[] AllowedExtensions { get; set; } =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".pdf",
            ".mp4"
        };

        public string[] AllowedMimeTypes { get; set; } =
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "application/pdf",
            "video/mp4"
        };
    }
}
