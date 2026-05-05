using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class TripEstimationService : ITripEstimationService
{
    private readonly IFlowReadingService? _flowReadingService;
    private readonly INearTermFlowOutlookService? _nearTermFlowOutlookService;

    /// <summary>
    /// Initializes a new instance of TripEstimationService.
    /// </summary>
    /// <param name="flowReadingService">Optional flow reading service for flow-aware estimation.
    /// If null, falls back to manual current input only.</param>
    public TripEstimationService(
        IFlowReadingService? flowReadingService = null,
        INearTermFlowOutlookService? nearTermFlowOutlookService = null)
    {
        _flowReadingService = flowReadingService;
        _nearTermFlowOutlookService = nearTermFlowOutlookService;
    }

    public TripEstimate Estimate(TripEstimateRequest request)
    {
        var currentSelection = GetRiverCurrentWithTraceability(request);
        string currentSource = GetCurrentSource(request, currentSelection.UsedFlowReading, currentSelection.UsedOutlook);
        string assumptions = GetAssumptions(currentSource, currentSelection.UsedOutlook);
        var conditionBasis = GetConditionBasis(currentSelection.UsedFlowReading, currentSelection.UsedOutlook);

        double effectiveSpeed = request.PaddlingSpeedMph + currentSelection.RiverCurrentMph;

        if (effectiveSpeed <= 0)
        {
            throw new ArgumentException("Effective speed must be greater than zero.");
        }

        double hours = request.DistanceMiles / effectiveSpeed;
        TimeSpan duration = TimeSpan.FromHours(hours);

        DateTime? finishTime = null;
        if (request.LaunchTimeLocal.HasValue)
        {
            finishTime = request.LaunchTimeLocal.Value.Add(duration);
        }

        return new TripEstimate
        {
            SegmentName = request.SegmentName,
            DistanceMiles = request.DistanceMiles,
            PaddlingSpeedMph = request.PaddlingSpeedMph,
            RiverCurrentMphUsed = currentSelection.RiverCurrentMph,
            CurrentMphUsed = currentSelection.RiverCurrentMph,
            EffectiveSpeedMph = effectiveSpeed,
            EstimatedDuration = duration,
            EstimatedFinishTimeLocal = finishTime,
            Assumptions = assumptions,
            CurrentSource = currentSource,
            ConditionBasis = conditionBasis.Label,
            ConditionBasisDetail = conditionBasis.Detail,
            ExplanationSummary = BuildExplanationSummary(
                request.PaddlingSpeedMph,
                currentSelection.RiverCurrentMph,
                effectiveSpeed,
                currentSource),
            // Flow traceability: populated only if flow data was actually used
            UsedFlowReadingId = currentSelection.UsedOutlook?.BasedOnFlowReadingId ?? currentSelection.UsedFlowReading?.Id,
            UsedFlowReadingTimestamp = currentSelection.UsedOutlook?.BasedOnObservedAtUtc ?? currentSelection.UsedFlowReading?.ObservedAtUtc,
            UsedFlowCurrentMph = currentSelection.UsedOutlook?.BasedOnCurrentMph ?? currentSelection.UsedFlowReading?.EstimatedCurrentMph,
            UsedFlowReadingSource = currentSelection.UsedOutlook?.BasedOnFlowReadingSource ?? currentSelection.UsedFlowReading?.Source
        };
    }

    /// <summary>
    /// Determines the river current and optionally the FlowReading used.
    /// Returns a tuple: (current value, flow reading if used or null).
    /// </summary>
    private CurrentSelection GetRiverCurrentWithTraceability(TripEstimateRequest request)
    {
        if (_nearTermFlowOutlookService != null)
        {
            var outlook = _nearTermFlowOutlookService.GetTomorrowMorningOutlook(request);
            if (outlook != null)
            {
                return new CurrentSelection(outlook.EstimatedCurrentMph, null, outlook);
            }
        }

        // If flow service is available and segment is specified, try to get flow data.
        if (_flowReadingService != null && request.SegmentId.HasValue)
        {
            var flowReading = _flowReadingService.GetLatestForSegment(request.SegmentId.Value);
            if (flowReading?.EstimatedCurrentMph.HasValue == true)
            {
                return new CurrentSelection(flowReading.EstimatedCurrentMph.Value, flowReading, null);
            }
        }

        // Fall back to manual override or 0 (no flow reading used).
        return new CurrentSelection(request.RiverCurrentMphOverride ?? 0, null, null);
    }

    /// <summary>
    /// Identifies the source of the current value used in estimation.
    /// </summary>
    private string GetCurrentSource(
        TripEstimateRequest request,
        FlowReading? usedFlowReading,
        NearTermFlowOutlook? usedOutlook)
    {
        if (usedOutlook != null)
        {
            return usedOutlook.Source;
        }

        if (usedFlowReading != null)
        {
            return "flow reading";
        }

        if (request.RiverCurrentMphOverride.HasValue)
        {
            return "manual current";
        }

        return "default zero current";
    }

    /// <summary>
    /// Generates assumption text reflecting the source of the current data.
    /// </summary>
    private string GetAssumptions(string currentSource, NearTermFlowOutlook? usedOutlook)
    {
        if (usedOutlook != null)
        {
            return usedOutlook.Assumptions;
        }

        if (currentSource == "flow reading")
        {
            return "This estimate uses river current from flow reading data.";
        }

        if (currentSource == "manual current")
        {
            return "This estimate uses manually entered river current.";
        }

        return "This estimate assumes zero river current (no flow data or manual input provided).";
    }

    private static ConditionBasisSummary GetConditionBasis(
        FlowReading? usedFlowReading,
        NearTermFlowOutlook? usedOutlook)
    {
        if (usedOutlook != null)
        {
            return new ConditionBasisSummary(
                "Planned-launch conditions",
                "Uses available near-term support for the selected launch time.");
        }

        if (usedFlowReading != null)
        {
            return new ConditionBasisSummary(
                "Latest available conditions",
                "Uses the latest stored flow reading for this run.");
        }

        return new ConditionBasisSummary(
            "Baseline conditions",
            "Uses seeded or manually provided current for this run.");
    }

    private string BuildExplanationSummary(
        double paddlingSpeedMph,
        double riverCurrentMph,
        double effectiveSpeedMph,
        string currentSource)
    {
        return $"Base paddling speed {paddlingSpeedMph:0.###} mph + current {riverCurrentMph:0.###} mph ({currentSource}) = effective {effectiveSpeedMph:0.###} mph.";
    }

    public string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours} hr {duration.Minutes} min";
    }

    private sealed record CurrentSelection(
        double RiverCurrentMph,
        FlowReading? UsedFlowReading,
        NearTermFlowOutlook? UsedOutlook);

    private sealed record ConditionBasisSummary(string Label, string Detail);
}
