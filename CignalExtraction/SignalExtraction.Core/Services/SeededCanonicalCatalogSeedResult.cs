namespace SignalExtraction.Core.Services;

public record SeededCanonicalCatalogSeedResult(
    int RunsInserted,
    int AccessPointsInserted,
    int AccessPointIdentifiersInserted,
    int GaugesInserted,
    int RunGaugeLinksInserted);
