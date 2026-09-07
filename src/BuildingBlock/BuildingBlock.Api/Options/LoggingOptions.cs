namespace BuildingBlock.Api.Options
{
    public sealed class LoggingOptions
    {
        public SamplingOptions? Sampling { get; set; }

        public sealed class SamplingOptions
        {
            public bool Enabled { get; set; }
            public double KeepRate { get; set; } = 1.0;
            public string MaxLevelToSample { get; set; } = "Information";
        }
    }
}
