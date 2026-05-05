using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface INearTermFlowOutlookService
{
    NearTermFlowOutlook? GetTomorrowMorningOutlook(TripEstimateRequest request);
}
