namespace SignalExtraction.Core.Services;

public interface IUsgsFlowImportService
{
    Task<UsgsFlowImportResult> ImportCurrentReadingsAsync(CancellationToken cancellationToken = default);
}
