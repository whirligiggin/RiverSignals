using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IProvisionalAccessPairService
{
    IReadOnlyList<ProvisionalAccessPair> GetProvisionalAccessPairs();

    ProvisionalAccessPair RecordProvisionalAccessPair(ProvisionalAccessPair pair);
}
