namespace SosExporter
{
    public enum ComputationPeriod
    {
        Unknown,
        QuarterHourly, // Not an AQTS stats period, but we use it to limit data
        // All the rest are stock AQTS stats periods
        None,
        Annual,
        Monthly,
        Weekly,
        Daily,
        Hourly,
        Points,
        WaterYear,
        Minutes,
    }
}
