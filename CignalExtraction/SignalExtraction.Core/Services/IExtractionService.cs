using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IExtractionService
{
    Task<ExtractionResult> ExtractAsync(ExtractionRequest request);
}