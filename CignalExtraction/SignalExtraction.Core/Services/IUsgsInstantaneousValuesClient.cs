using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IUsgsInstantaneousValuesClient
{
    Task<IReadOnlyList<UsgsGaugeInstantaneousReading>> GetCurrentReadingsAsync(
        IEnumerable<UsgsGauge> gauges,
        CancellationToken cancellationToken = default);
}
