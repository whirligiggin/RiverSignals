namespace SignalExtraction.Core.Models;

public class River
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
}