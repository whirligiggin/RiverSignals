using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IAccessPointIdentifierService
{
    IReadOnlyList<AccessPointIdentifier> GetAccessPointIdentifiers();
}
