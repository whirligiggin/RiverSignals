namespace SignalExtraction.Core.Models;

public record CanonicalWorkbenchTable(
    string Name,
    string Label,
    IReadOnlyList<string> EditableFields);

public record CanonicalWorkbenchRecord(
    string TableName,
    string Id,
    IReadOnlyDictionary<string, string?> Values);

public class CanonicalWorkbenchUpdateRequest
{
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
