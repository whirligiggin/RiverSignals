using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface ISegmentCatalogService
{
    List<River> GetPresetRivers();
    List<AccessPoint> GetPresetAccessPoints();
    List<UsgsGauge> GetPresetUsgsGauges();
    List<UsgsGaugeImportTarget> GetPresetUsgsGaugeImportTargets();
    List<SegmentRiverMileDistanceComparison> GetRiverMileDistanceComparisons();
    List<Segment> GetPresetSegments();
    Segment? GetSegment(Guid segmentId);
}
