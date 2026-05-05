using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/gauges/import-targets")]
public class GaugeImportTargetsController : ControllerBase
{
    private readonly ISegmentCatalogService _segmentCatalogService;

    public GaugeImportTargetsController(ISegmentCatalogService segmentCatalogService)
    {
        _segmentCatalogService = segmentCatalogService;
    }

    [HttpGet]
    public ActionResult<List<GaugeImportTargetResponse>> GetImportTargets()
    {
        var segmentsById = _segmentCatalogService.GetPresetSegments().ToDictionary(segment => segment.Id);
        var gaugesById = _segmentCatalogService.GetPresetUsgsGauges().ToDictionary(gauge => gauge.Id);

        var targets = _segmentCatalogService
            .GetPresetUsgsGaugeImportTargets()
            .Where(target => gaugesById.ContainsKey(target.GaugeId) && segmentsById.ContainsKey(target.SegmentId))
            .Select(target =>
            {
                var gauge = gaugesById[target.GaugeId];
                var segment = segmentsById[target.SegmentId];

                return new GaugeImportTargetResponse(
                    target.GaugeId,
                    gauge.StationId,
                    gauge.Name,
                    target.SegmentId,
                    segment.Name,
                    target.RelationshipType,
                    target.ReviewStatus,
                    target.Notes);
            })
            .OrderBy(target => target.GaugeName, StringComparer.Ordinal)
            .ThenBy(target => target.SegmentName, StringComparer.Ordinal)
            .ToList();

        return Ok(targets);
    }
}

public record GaugeImportTargetResponse(
    Guid GaugeId,
    string StationId,
    string GaugeName,
    Guid SegmentId,
    string SegmentName,
    UsgsGaugeRelationshipType RelationshipType,
    UsgsGaugeLinkageReviewStatus ReviewStatus,
    string? Notes);
