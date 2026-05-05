using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class UsgsInstantaneousValuesClient : IUsgsInstantaneousValuesClient
{
    private readonly HttpClient _httpClient;

    public UsgsInstantaneousValuesClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<UsgsGaugeInstantaneousReading>> GetCurrentReadingsAsync(
        IEnumerable<UsgsGauge> gauges,
        CancellationToken cancellationToken = default)
    {
        var gaugeList = gauges
            .Where(gauge => !string.IsNullOrWhiteSpace(gauge.StationId))
            .ToList();

        if (gaugeList.Count == 0)
            return [];

        var stationIds = string.Join(",", gaugeList.Select(gauge => gauge.StationId.Trim()));
        var requestUri = $"nwis/iv/?format=json&sites={stationIds}&parameterCd=00060,00065&siteStatus=all";
        var json = await _httpClient.GetStringAsync(requestUri, cancellationToken);

        return UsgsInstantaneousValuesJsonParser.Parse(json, gaugeList);
    }
}
