using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/segments")]
public class SegmentsController : ControllerBase
{
    private const double DefaultPaddlingSpeedMph = 3.0;

    private readonly ISegmentCatalogService _segmentCatalogService;
    private readonly IFlowReadingService _flowReadingService;
    private readonly ITripObservationService _tripObservationService;
    private readonly ITripEstimationService _tripEstimationService;

    public SegmentsController(
        ISegmentCatalogService segmentCatalogService,
        IFlowReadingService flowReadingService,
        ITripObservationService tripObservationService,
        ITripEstimationService tripEstimationService)
    {
        _segmentCatalogService = segmentCatalogService;
        _flowReadingService = flowReadingService;
        _tripObservationService = tripObservationService;
        _tripEstimationService = tripEstimationService;
    }

    [HttpGet]
    public ActionResult<List<SegmentResponse>> GetSegments()
    {
        var segments = _segmentCatalogService
            .GetPresetSegments()
            .Where(segment => segment.IsActive)
            .Select(ToSegmentResponse)
            .ToList();

        return Ok(segments);
    }

    [HttpGet("{segmentId:guid}")]
    public ActionResult<SegmentPlanningResponse> GetSegment(Guid segmentId)
    {
        var segment = _segmentCatalogService.GetSegment(segmentId);
        if (segment == null || !segment.IsActive)
            return NotFound($"No segment found for {segmentId}.");

        var latestFlow = _flowReadingService.GetLatestForSegment(segmentId);

        return Ok(new SegmentPlanningResponse(
            ToSegmentResponse(segment),
            latestFlow == null ? null : ToFlowSummary(latestFlow)));
    }

    [HttpPost("{segmentId:guid}/estimate")]
    public ActionResult<SegmentEstimateResponse> Estimate(
        Guid segmentId,
        [FromBody] SegmentEstimateRequest? request)
    {
        var segment = _segmentCatalogService.GetSegment(segmentId);
        if (segment == null || !segment.IsActive)
            return NotFound($"No segment found for {segmentId}.");

        var paddlingSpeedMph = request?.PaddlingSpeedMph ?? DefaultPaddlingSpeedMph;
        if (paddlingSpeedMph <= 0)
            return BadRequest("PaddlingSpeedMph must be greater than zero.");

        var estimate = _tripEstimationService.Estimate(new TripEstimateRequest
        {
            SegmentId = segment.Id,
            SegmentName = segment.Name,
            DistanceMiles = segment.DistanceMiles,
            PaddlingSpeedMph = paddlingSpeedMph,
            RiverCurrentMphOverride = segment.DefaultCurrentMph ?? 0,
            LaunchTimeLocal = request?.LaunchTimeLocal
        });

        var latestFlow = _flowReadingService.GetLatestForSegment(segment.Id);

        return Ok(new SegmentEstimateResponse(
            ToSegmentResponse(segment),
            latestFlow == null ? null : ToFlowSummary(latestFlow),
            estimate));
    }

    [HttpPost("{segmentId:guid}/observations")]
    public ActionResult<TripObservationResponse> AddObservation(
        Guid segmentId,
        [FromBody] TripObservationRequest? request)
    {
        var segment = _segmentCatalogService.GetSegment(segmentId);
        if (segment == null || !segment.IsActive)
            return NotFound($"No segment found for {segmentId}.");

        if (request == null)
            return BadRequest("Observation payload is required.");

        if (request.StartTimeLocal == default)
            return BadRequest("StartTimeLocal is required.");

        if (request.FinishTimeLocal == null && request.DurationMinutes == null)
            return BadRequest("Provide either FinishTimeLocal or DurationMinutes.");

        if (request.DurationMinutes.HasValue && request.DurationMinutes <= 0)
            return BadRequest("DurationMinutes must be greater than zero when provided.");

        if (request.FinishTimeLocal.HasValue && request.FinishTimeLocal <= request.StartTimeLocal)
            return BadRequest("FinishTimeLocal must be later than StartTimeLocal.");

        var observation = new TripObservation
        {
            Id = Guid.NewGuid(),
            SegmentId = segment.Id,
            ReviewState = ObservationReviewState.Unreviewed,
            PipelineStage = ObservationPipelineStage.Structured,
            StartTimeLocal = request.StartTimeLocal,
            FinishTimeLocal = request.FinishTimeLocal,
            DurationMinutes = request.DurationMinutes,
            PutInText = request.PutInText,
            TakeOutText = request.TakeOutText,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow
        };

        var stored = _tripObservationService.AddObservation(observation);

        return Ok(new TripObservationResponse(
            stored.Id,
            stored.SegmentId,
            segment.Name,
            stored.ReviewState,
            stored.PipelineStage,
            stored.StartTimeLocal,
            stored.FinishTimeLocal,
            stored.DurationMinutes,
            stored.PutInText,
            stored.TakeOutText,
            stored.Notes,
            stored.CreatedAtUtc));
    }

    private static SegmentResponse ToSegmentResponse(Segment segment)
    {
        return new SegmentResponse(
            segment.Id,
            segment.RiverId,
            segment.Name,
            segment.PutInName,
            segment.TakeOutName,
            segment.DistanceMiles,
            segment.DistanceSource,
            segment.PutInRiverMile,
            segment.TakeOutRiverMile,
            segment.PutInAddress,
            segment.TakeOutAddress,
            segment.PutInAmenities,
            segment.TakeOutAmenities,
            segment.PlanningSource,
            segment.DefaultCurrentMph,
            segment.IsActive);
    }

    private static FlowSummaryResponse ToFlowSummary(FlowReading reading)
    {
        return new FlowSummaryResponse(
            reading.Id,
            reading.SegmentId,
            reading.ObservedAtUtc,
            reading.EstimatedCurrentMph,
            reading.FlowRateCfs,
            reading.Source);
    }
}

public class SegmentEstimateRequest
{
    public double? PaddlingSpeedMph { get; set; }
    public DateTime? LaunchTimeLocal { get; set; }
}

public class TripObservationRequest
{
    public DateTime StartTimeLocal { get; set; }
    public DateTime? FinishTimeLocal { get; set; }
    public int? DurationMinutes { get; set; }
    public string? PutInText { get; set; }
    public string? TakeOutText { get; set; }
    public string? Notes { get; set; }
}

public record SegmentResponse(
    Guid Id,
    Guid RiverId,
    string Name,
    string PutInName,
    string TakeOutName,
    double DistanceMiles,
    string? DistanceSource,
    double? PutInRiverMile,
    double? TakeOutRiverMile,
    string? PutInAddress,
    string? TakeOutAddress,
    string? PutInAmenities,
    string? TakeOutAmenities,
    string? PlanningSource,
    double? DefaultCurrentMph,
    bool IsActive);

public record SegmentPlanningResponse(
    SegmentResponse Segment,
    FlowSummaryResponse? LatestFlow);

public record SegmentEstimateResponse(
    SegmentResponse Segment,
    FlowSummaryResponse? LatestFlow,
    TripEstimate Estimate);

public record TripObservationResponse(
    Guid Id,
    Guid SegmentId,
    string SegmentName,
    ObservationReviewState ReviewState,
    ObservationPipelineStage PipelineStage,
    DateTime StartTimeLocal,
    DateTime? FinishTimeLocal,
    int? DurationMinutes,
    string? PutInText,
    string? TakeOutText,
    string? Notes,
    DateTime CreatedAtUtc);

public record FlowSummaryResponse(
    Guid Id,
    Guid SegmentId,
    DateTime ObservedAtUtc,
    double? EstimatedCurrentMph,
    double? FlowRateCfs,
    string Source);
