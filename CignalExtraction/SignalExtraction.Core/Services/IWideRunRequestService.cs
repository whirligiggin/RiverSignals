using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IWideRunRequestService
{
    StoredWideRunRequest Store(WideRunRequest request);
}
