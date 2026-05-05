using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class UsgsFlowImportService : IUsgsFlowImportService
{
    private readonly ISegmentCatalogService _segmentCatalogService;
    private readonly IUsgsInstantaneousValuesClient _usgsClient;
    private readonly IFlowReadingService _flowReadingService;

    public UsgsFlowImportService(
        ISegmentCatalogService segmentCatalogService,
        IUsgsInstantaneousValuesClient usgsClient,
        IFlowReadingService flowReadingService)
    {
        _segmentCatalogService = segmentCatalogService;
        _usgsClient = usgsClient;
        _flowReadingService = flowReadingService;
    }

    public async Task<UsgsFlowImportResult> ImportCurrentReadingsAsync(CancellationToken cancellationToken = default)
    {
        var gauges = _segmentCatalogService.GetPresetUsgsGauges();
        var importTargetsByGaugeId = _segmentCatalogService
            .GetPresetUsgsGaugeImportTargets()
            .GroupBy(target => target.GaugeId)
            .ToDictionary(group => group.Key, group => group.Select(target => target.SegmentId).ToList());

        var gaugesToQuery = gauges
            .Where(gauge => importTargetsByGaugeId.ContainsKey(gauge.Id))
            .ToList();

        var readings = await _usgsClient.GetCurrentReadingsAsync(gaugesToQuery, cancellationToken);
        var result = new UsgsFlowImportResult
        {
            GaugesQueried = gaugesToQuery.Count,
            GaugeReadingsReceived = readings.Count
        };

        foreach (var reading in readings)
        {
            if (!importTargetsByGaugeId.TryGetValue(reading.GaugeId, out var segmentIds))
                continue;

            foreach (var segmentId in segmentIds)
            {
                var flowReading = new FlowReading
                {
                    Id = Guid.NewGuid(),
                    SegmentId = segmentId,
                    ObservedAtUtc = reading.ObservedAtUtc,
                    GaugeHeightFeet = reading.GaugeHeightFeet,
                    FlowRateCfs = reading.FlowRateCfs,
                    EstimatedCurrentMph = null,
                    Source = "USGS",
                    SourceReference = reading.StationId
                };

                _flowReadingService.AddFlowReading(flowReading);
                result.FlowReadingsStored++;
                result.ImportedReadings.Add(new ImportedSegmentFlowReading
                {
                    SegmentId = segmentId,
                    GaugeId = reading.GaugeId,
                    StationId = reading.StationId,
                    GaugeName = reading.GaugeName,
                    ObservedAtUtc = reading.ObservedAtUtc,
                    GaugeHeightFeet = reading.GaugeHeightFeet,
                    FlowRateCfs = reading.FlowRateCfs
                });
            }
        }

        return result;
    }
}
