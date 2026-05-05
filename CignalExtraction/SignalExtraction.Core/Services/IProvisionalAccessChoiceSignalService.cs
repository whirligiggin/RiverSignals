using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IProvisionalAccessChoiceSignalService
{
    IReadOnlyList<ProvisionalAccessChoiceSignal> GetProvisionalAccessChoiceSignals();

    ProvisionalAccessChoiceSignal RecordChoiceSignal(ProvisionalAccessChoiceSignal signal);
}
