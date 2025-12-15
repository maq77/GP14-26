namespace SSSP.BL.Options
{
    public sealed class StartupValidationOptions
    {
        public bool Enabled { get; set; } = true;
        public bool EmitTelemetry { get; set; } = true;
        public bool FailFastInDevelopment { get; set; } = false;
        public int MaxDbRetriesDevelopment { get; set; } = 3;
        public int MaxDbRetriesProduction { get; set; } = 10;
        public int DbRetryDelaySeconds { get; set; } = 10;
    }
}
