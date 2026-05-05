using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class NearTermFlowOutlookService : INearTermFlowOutlookService
{
    private readonly ISegmentCatalogService _segmentCatalogService;
    private readonly IFlowReadingService _flowReadingService;

    public NearTermFlowOutlookService(
        ISegmentCatalogService segmentCatalogService,
        IFlowReadingService flowReadingService)
    {
        _segmentCatalogService = segmentCatalogService;
        _flowReadingService = flowReadingService;
    }

    public NearTermFlowOutlook? GetTomorrowMorningOutlook(TripEstimateRequest request)
    {
        if (!request.SegmentId.HasValue || !request.LaunchTimeLocal.HasValue)
            return null;

        var hasExplicitMapping = _segmentCatalogService
            .GetPresetUsgsGaugeImportTargets()
            .Any(target => target.SegmentId == request.SegmentId.Value);

        if (!hasExplicitMapping)
            return null;

        var latestFlow = _flowReadingService.GetLatestForSegment(request.SegmentId.Value);
        if (latestFlow?.EstimatedCurrentMph.HasValue != true)
            return null;

        if (!string.Equals(latestFlow.Source, "USGS", StringComparison.OrdinalIgnoreCase))
            return null;

        var estimatedCurrentMph = latestFlow.EstimatedCurrentMph;
        if (!estimatedCurrentMph.HasValue)
            return null;

        var observedLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(latestFlow.ObservedAtUtc, DateTimeKind.Utc),
            TimeZoneInfo.Local);

        if (!IsTomorrowMorningLaunchRelativeToObservedReading(request.LaunchTimeLocal.Value, observedLocal))
            return null;

        return new NearTermFlowOutlook
        {
            SegmentId = request.SegmentId.Value,
            LaunchTimeLocal = request.LaunchTimeLocal.Value,
            EstimatedCurrentMph = estimatedCurrentMph.Value,
            Source = "tomorrow-morning outlook",
            Assumptions = "This estimate uses a bounded tomorrow-morning outlook carried forward from the latest explicitly mapped USGS segment reading.",
            BasedOnFlowReadingId = latestFlow.Id,
            BasedOnObservedAtUtc = latestFlow.ObservedAtUtc,
            BasedOnCurrentMph = estimatedCurrentMph.Value,
            BasedOnFlowReadingSource = latestFlow.Source
        };
    }

    private static bool IsTomorrowMorningLaunchRelativeToObservedReading(DateTime launchTimeLocal, DateTime observedLocal)
    {
        return launchTimeLocal.Date == observedLocal.Date.AddDays(1)
            && launchTimeLocal.TimeOfDay >= TimeSpan.FromHours(5)
            && launchTimeLocal.TimeOfDay < TimeSpan.FromHours(12);
    }
}
