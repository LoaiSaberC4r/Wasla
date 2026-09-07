namespace BuildingBlock.Infrastructure.Options
{
    public sealed class QrCodeOptions
    {
        public int MaximumPayloadBytes { get; set; } = 4096;

        public int MinimumPixelsPerModule { get; set; } = 1;

        public int MaximumPixelsPerModule { get; set; } = 40;
    }
}
