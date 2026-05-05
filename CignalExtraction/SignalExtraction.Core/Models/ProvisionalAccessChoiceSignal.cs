namespace SignalExtraction.Core.Models;

public class ProvisionalAccessChoiceSignal
{
    public Guid Id { get; set; }
    public Guid? RawAccessPointCandidateId { get; set; }
    public string? PossibleAccessKey { get; set; }
    public ProvisionalAccessChoiceSignalSource SignalSource { get; set; }
    public DateTime SelectedAtUtc { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? Note { get; set; }
}

public enum ProvisionalAccessChoiceSignalSource
{
    UserSelected,
    StewardSelected
}
