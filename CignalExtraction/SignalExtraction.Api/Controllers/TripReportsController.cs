using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/trip-reports")]
public class TripReportsController : ControllerBase
{
    private readonly IExtractionService _extractionService;
    private readonly ExtractionPlanningHandoffEvaluator _handoffEvaluator;

    public TripReportsController(
        IExtractionService extractionService,
        ExtractionPlanningHandoffEvaluator handoffEvaluator)
    {
        _extractionService = extractionService;
        _handoffEvaluator = handoffEvaluator;
    }

    [HttpPost("extract")]
    public async Task<ActionResult<TripReportExtractionResponse>> Extract(
        [FromBody] TripReportExtractionRequest? request)
    {
        if (request == null)
            return BadRequest("Trip report payload is required.");

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text is required.");

        var result = await _extractionService.ExtractAsync(new ExtractionRequest
        {
            Text = request.Text,
            SourceType = request.SourceType ?? SourceType.ManualPaste,
            CommunicationDateTime = request.CommunicationDateTime
        });

        return Ok(ToResponse(request.Text, result));
    }

    [HttpPost("planning-handoff")]
    public ActionResult<TripReportPlanningHandoffResponse> PlanningHandoff(
        [FromBody] TripReportPlanningHandoffRequest? request)
    {
        if (request == null)
            return BadRequest("Planning handoff payload is required.");

        if (request.Extraction == null)
            return BadRequest("Extraction facts are required.");

        var result = _handoffEvaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = request.Extraction.ToExtractionResult(),
            SegmentId = request.SegmentId,
            SegmentName = request.SegmentName,
            DistanceMiles = request.DistanceMiles,
            PaddlingSpeedMph = request.PaddlingSpeedMph,
            RiverCurrentMphOverride = request.RiverCurrentMphOverride,
            LaunchTimeLocal = request.LaunchTimeLocal
        });

        return Ok(ToResponse(result));
    }

    private static TripReportExtractionResponse ToResponse(string sourceText, ExtractionResult result)
    {
        return new TripReportExtractionResponse(
            SourceText: sourceText,
            RecordType: result.RecordType,
            PutIn: result.PutInLocation,
            PutInSourceText: result.PutInSourceText,
            PullOut: result.PullOutLocation,
            PullOutSourceText: result.PullOutSourceText,
            WatercraftType: result.WatercraftType,
            WatercraftSourceText: result.WatercraftSourceText,
            DurationHours: result.DurationHours,
            DurationText: result.DurationSourceText,
            DurationType: result.DurationType,
            TripDateOrTiming: result.TripDateOrTiming,
            TripDateOrTimingSourceText: result.TripDateOrTimingSourceText,
            ConditionsOrNotes: result.ConditionsOrNotes,
            SourceType: result.SourceType,
            CommunicationDateTime: result.CommunicationDateTime,
            Confidence: result.ExtractionConfidence,
            NeedsReview: result.NeedsReview,
            ReviewFlags: result.ReviewReasons);
    }

    private static TripReportPlanningHandoffResponse ToResponse(ExtractionPlanningHandoffResult result)
    {
        return new TripReportPlanningHandoffResponse(
            CanEstimate: result.CanEstimate,
            SegmentId: result.SegmentId,
            SegmentName: result.SegmentName,
            DistanceMiles: result.DistanceMiles,
            PaddlingSpeedMph: result.PaddlingSpeedMph,
            RiverCurrentMphOverride: result.RiverCurrentMphOverride,
            LaunchTimeLocal: result.LaunchTimeLocal,
            AvailableInputs: result.AvailableInputs,
            MissingInputs: result.MissingInputs,
            AmbiguityFlags: result.AmbiguityFlags,
            Summary: result.Summary);
    }
}

public class TripReportExtractionRequest
{
    public string Text { get; set; } = string.Empty;
    public SourceType? SourceType { get; set; }
    public DateTime? CommunicationDateTime { get; set; }
}

public record TripReportExtractionResponse(
    string SourceText,
    RecordType RecordType,
    string? PutIn,
    string? PutInSourceText,
    string? PullOut,
    string? PullOutSourceText,
    string? WatercraftType,
    string? WatercraftSourceText,
    double? DurationHours,
    string? DurationText,
    DurationType DurationType,
    string? TripDateOrTiming,
    string? TripDateOrTimingSourceText,
    string? ConditionsOrNotes,
    SourceType SourceType,
    DateTime? CommunicationDateTime,
    double Confidence,
    bool NeedsReview,
    List<string> ReviewFlags);

public class TripReportPlanningHandoffRequest
{
    public TripReportPlanningExtractionFacts? Extraction { get; set; }
    public Guid? SegmentId { get; set; }
    public string? SegmentName { get; set; }
    public double? DistanceMiles { get; set; }
    public double? PaddlingSpeedMph { get; set; }
    public double? RiverCurrentMphOverride { get; set; }
    public DateTime? LaunchTimeLocal { get; set; }
}

public class TripReportPlanningExtractionFacts
{
    public string? PutIn { get; set; }
    public string? PutInSourceText { get; set; }
    public string? PullOut { get; set; }
    public string? PullOutSourceText { get; set; }
    public string? WatercraftType { get; set; }
    public string? WatercraftSourceText { get; set; }
    public double? DurationHours { get; set; }
    public string? DurationText { get; set; }
    public DurationType DurationType { get; set; } = DurationType.Unknown;
    public string? TripDateOrTiming { get; set; }
    public string? TripDateOrTimingSourceText { get; set; }
    public string? ConditionsOrNotes { get; set; }
    public RecordType RecordType { get; set; } = RecordType.UnclearTripRecord;
    public SourceType SourceType { get; set; } = SourceType.ManualPaste;
    public DateTime? CommunicationDateTime { get; set; }
    public double Confidence { get; set; }
    public bool NeedsReview { get; set; }
    public List<string> ReviewFlags { get; set; } = new();

    public ExtractionResult ToExtractionResult()
    {
        return new ExtractionResult
        {
            PutInLocation = PutIn,
            PutInSourceText = PutInSourceText,
            PullOutLocation = PullOut,
            PullOutSourceText = PullOutSourceText,
            WatercraftType = WatercraftType,
            WatercraftSourceText = WatercraftSourceText,
            DurationHours = DurationHours,
            DurationSourceText = DurationText,
            DurationType = DurationType,
            TripDateOrTiming = TripDateOrTiming,
            TripDateOrTimingSourceText = TripDateOrTimingSourceText,
            ConditionsOrNotes = ConditionsOrNotes,
            RecordType = RecordType,
            SourceType = SourceType,
            CommunicationDateTime = CommunicationDateTime,
            ExtractionConfidence = Confidence,
            NeedsReview = NeedsReview,
            ReviewReasons = ReviewFlags
        };
    }
}

public record TripReportPlanningHandoffResponse(
    bool CanEstimate,
    Guid? SegmentId,
    string? SegmentName,
    double? DistanceMiles,
    double? PaddlingSpeedMph,
    double? RiverCurrentMphOverride,
    DateTime? LaunchTimeLocal,
    List<string> AvailableInputs,
    List<string> MissingInputs,
    List<string> AmbiguityFlags,
    string Summary);
