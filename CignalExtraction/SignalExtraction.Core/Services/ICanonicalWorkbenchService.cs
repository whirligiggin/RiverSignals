using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface ICanonicalWorkbenchService
{
    IReadOnlyList<CanonicalWorkbenchTable> GetTables();
    IReadOnlyList<CanonicalWorkbenchRecord> GetRecords(string tableName);
    CanonicalWorkbenchRecord? UpdateRecord(string tableName, string id, IReadOnlyDictionary<string, string?> values);
}
