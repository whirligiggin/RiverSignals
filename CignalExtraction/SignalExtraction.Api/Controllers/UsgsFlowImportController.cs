using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/flow/import/usgs")]
public class UsgsFlowImportController : ControllerBase
{
    private readonly IUsgsFlowImportService _usgsFlowImportService;

    public UsgsFlowImportController(IUsgsFlowImportService usgsFlowImportService)
    {
        _usgsFlowImportService = usgsFlowImportService;
    }

    [HttpPost("current")]
    public async Task<ActionResult<UsgsFlowImportResponse>> ImportCurrent(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _usgsFlowImportService.ImportCurrentReadingsAsync(cancellationToken);

            return Ok(new UsgsFlowImportResponse(
                result.GaugesQueried,
                result.GaugeReadingsReceived,
                result.FlowReadingsStored,
                result.ImportedReadings
                    .OrderBy(reading => reading.GaugeName, StringComparer.Ordinal)
                    .ThenBy(reading => reading.SegmentId)
                    .Select(reading => new ImportedFlowReadingResponse(
                        reading.SegmentId,
                        reading.GaugeId,
                        reading.StationId,
                        reading.GaugeName,
                        reading.ObservedAtUtc,
                        reading.GaugeHeightFeet,
                        reading.FlowRateCfs))
                    .ToList()));
        }
        catch (HttpRequestException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, exception.Message);
        }
    }
}

public record UsgsFlowImportResponse(
    int GaugesQueried,
    int GaugeReadingsReceived,
    int FlowReadingsStored,
    List<ImportedFlowReadingResponse> ImportedReadings);

public record ImportedFlowReadingResponse(
    Guid SegmentId,
    Guid GaugeId,
    string StationId,
    string GaugeName,
    DateTime ObservedAtUtc,
    double? GaugeHeightFeet,
    double? FlowRateCfs);
