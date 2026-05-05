using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/flow")]
public class FlowController : ControllerBase
{
    private readonly IFlowReadingService _flowReadingService;

    public FlowController(IFlowReadingService flowReadingService)
    {
        _flowReadingService = flowReadingService;
    }

    [HttpGet("{segmentId:guid}")]
    public ActionResult<FlowReadingResponse> GetLatest(Guid segmentId)
    {
        var reading = _flowReadingService.GetLatestForSegment(segmentId);
        if (reading == null)
            return NotFound($"No flow reading found for segment {segmentId}.");

        return Ok(ToResponse(reading));
    }

    [HttpPost]
    public ActionResult<FlowIngestionResponse> Post([FromBody] FlowIngestionRequest? request)
    {
        var validationError = Validate(request);
        if (validationError != null)
            return BadRequest(validationError);

        var validRequest = request!;
        var reading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = validRequest.SegmentId,
            ObservedAtUtc = validRequest.ObservedAtUtc,
            EstimatedCurrentMph = validRequest.EstimatedCurrentMph,
            GaugeHeightFeet = validRequest.GaugeHeightFeet,
            FlowRateCfs = validRequest.FlowRateCfs,
            Source = validRequest.Source.Trim(),
            SourceReference = string.IsNullOrWhiteSpace(validRequest.SourceReference)
                ? null
                : validRequest.SourceReference.Trim()
        };

        _flowReadingService.AddFlowReading(reading);

        return Ok(new FlowIngestionResponse(
            reading.Id,
            reading.SegmentId,
            reading.ObservedAtUtc,
            reading.EstimatedCurrentMph,
            reading.FlowRateCfs,
            reading.Source));
    }

    private static FlowReadingResponse ToResponse(FlowReading reading)
    {
        return new FlowReadingResponse(
            reading.Id,
            reading.SegmentId,
            reading.ObservedAtUtc,
            reading.EstimatedCurrentMph,
            reading.GaugeHeightFeet,
            reading.FlowRateCfs,
            reading.Source,
            reading.SourceReference);
    }

    private static string? Validate(FlowIngestionRequest? request)
    {
        if (request == null)
            return "Flow reading payload is required.";

        if (request.SegmentId == Guid.Empty)
            return "SegmentId is required.";

        if (request.ObservedAtUtc == default)
            return "ObservedAtUtc is required.";

        if (string.IsNullOrWhiteSpace(request.Source))
            return "Source is required.";

        if (!request.EstimatedCurrentMph.HasValue &&
            !request.FlowRateCfs.HasValue &&
            !request.GaugeHeightFeet.HasValue)
        {
            return "At least one flow measurement is required.";
        }

        return null;
    }
}

public class FlowIngestionRequest
{
    public Guid SegmentId { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public double? FlowRateCfs { get; set; }
    public double? EstimatedCurrentMph { get; set; }
    public double? GaugeHeightFeet { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
}

public record FlowIngestionResponse(
    Guid Id,
    Guid SegmentId,
    DateTime ObservedAtUtc,
    double? EstimatedCurrentMph,
    double? FlowRateCfs,
    string Source);

public record FlowReadingResponse(
    Guid Id,
    Guid SegmentId,
    DateTime ObservedAtUtc,
    double? EstimatedCurrentMph,
    double? GaugeHeightFeet,
    double? FlowRateCfs,
    string Source,
    string? SourceReference);
