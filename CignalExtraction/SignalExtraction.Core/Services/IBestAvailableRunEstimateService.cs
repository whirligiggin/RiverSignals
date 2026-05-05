using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IBestAvailableRunEstimateService
{
    WideRunRequestSubmissionResult Estimate(WideRunRequest request, StoredWideRunRequest storedRequest);
}
