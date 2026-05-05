namespace SignalExtraction.Core.Models;

public class ExtractionRequest
{
    public string Text { get; set; } = string.Empty;

    public SourceType SourceType { get; set; }

    public DateTime? CommunicationDateTime { get; set; }
}