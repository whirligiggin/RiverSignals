using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/gauges")]
public class GaugesController : ControllerBase
{
    private readonly ISegmentCatalogService _segmentCatalogService;

    public GaugesController(ISegmentCatalogService segmentCatalogService)
    {
        _segmentCatalogService = segmentCatalogService;
    }

    [HttpGet]
    public ActionResult<List<UsgsGaugeResponse>> GetGauges()
    {
        var gauges = _segmentCatalogService
            .GetPresetUsgsGauges()
            .OrderBy(gauge => gauge.Name)
            .Select(gauge => new UsgsGaugeResponse(
                gauge.Id,
                gauge.RiverId,
                gauge.StationId,
                gauge.Name,
                gauge.Source,
                gauge.SourceReference,
                gauge.Notes))
            .ToList();

        return Ok(gauges);
    }
}

public record UsgsGaugeResponse(
    Guid Id,
    Guid RiverId,
    string StationId,
    string Name,
    string Source,
    string SourceReference,
    string? Notes);
