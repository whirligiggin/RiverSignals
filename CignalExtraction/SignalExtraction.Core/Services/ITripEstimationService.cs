using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface ITripEstimationService
{
    TripEstimate Estimate(TripEstimateRequest request);
    string FormatDuration(TimeSpan duration);
}