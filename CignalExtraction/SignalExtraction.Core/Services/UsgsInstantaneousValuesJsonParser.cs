using System.Globalization;
using System.Text.Json;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public static class UsgsInstantaneousValuesJsonParser
{
    private const string DischargeParameterCode = "00060";
    private const string GaugeHeightParameterCode = "00065";

    public static IReadOnlyList<UsgsGaugeInstantaneousReading> Parse(string json, IEnumerable<UsgsGauge> gauges)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(gauges);

        var gaugesByStationId = gauges.ToDictionary(gauge => gauge.StationId, StringComparer.Ordinal);
        var builderByStationId = new Dictionary<string, GaugeReadingBuilder>(StringComparer.Ordinal);

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("value", out var valueNode) ||
            !valueNode.TryGetProperty("timeSeries", out var timeSeriesNode) ||
            timeSeriesNode.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        foreach (var timeSeries in timeSeriesNode.EnumerateArray())
        {
            var stationId = ReadStationId(timeSeries);
            if (stationId == null || !gaugesByStationId.TryGetValue(stationId, out var gauge))
                continue;

            var parameterCode = ReadParameterCode(timeSeries);
            if (parameterCode == null)
                continue;

            var latestValue = ReadLatestMeasurement(timeSeries);
            if (latestValue == null)
                continue;

            if (!builderByStationId.TryGetValue(stationId, out var builder))
            {
                builder = new GaugeReadingBuilder
                {
                    GaugeId = gauge.Id,
                    StationId = gauge.StationId,
                    GaugeName = gauge.Name
                };
                builderByStationId[stationId] = builder;
            }

            builder.ObservedAtUtc = builder.ObservedAtUtc.HasValue && builder.ObservedAtUtc > latestValue.Value.ObservedAtUtc
                ? builder.ObservedAtUtc
                : latestValue.Value.ObservedAtUtc;

            if (parameterCode == DischargeParameterCode)
                builder.FlowRateCfs = latestValue.Value.NumericValue;
            else if (parameterCode == GaugeHeightParameterCode)
                builder.GaugeHeightFeet = latestValue.Value.NumericValue;
        }

        return builderByStationId.Values
            .Where(builder => builder.ObservedAtUtc.HasValue)
            .Select(builder => new UsgsGaugeInstantaneousReading
            {
                GaugeId = builder.GaugeId,
                StationId = builder.StationId,
                GaugeName = builder.GaugeName,
                ObservedAtUtc = builder.ObservedAtUtc!.Value,
                GaugeHeightFeet = builder.GaugeHeightFeet,
                FlowRateCfs = builder.FlowRateCfs
            })
            .OrderBy(reading => reading.StationId, StringComparer.Ordinal)
            .ToList();
    }

    private static string? ReadStationId(JsonElement timeSeries)
    {
        if (!timeSeries.TryGetProperty("sourceInfo", out var sourceInfo) ||
            !sourceInfo.TryGetProperty("siteCode", out var siteCodeNode) ||
            siteCodeNode.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var siteCode in siteCodeNode.EnumerateArray())
        {
            if (siteCode.TryGetProperty("value", out var valueNode))
            {
                var stationId = valueNode.GetString();
                if (!string.IsNullOrWhiteSpace(stationId))
                    return stationId.Trim();
            }
        }

        return null;
    }

    private static string? ReadParameterCode(JsonElement timeSeries)
    {
        if (!timeSeries.TryGetProperty("variable", out var variableNode) ||
            !variableNode.TryGetProperty("variableCode", out var variableCodeNode) ||
            variableCodeNode.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var variableCode in variableCodeNode.EnumerateArray())
        {
            if (variableCode.TryGetProperty("value", out var valueNode))
            {
                var parameterCode = valueNode.GetString();
                if (!string.IsNullOrWhiteSpace(parameterCode))
                    return parameterCode.Trim();
            }
        }

        return null;
    }

    private static ParsedMeasurement? ReadLatestMeasurement(JsonElement timeSeries)
    {
        if (!timeSeries.TryGetProperty("values", out var valuesNode) || valuesNode.ValueKind != JsonValueKind.Array)
            return null;

        ParsedMeasurement? latest = null;

        foreach (var valuesBlock in valuesNode.EnumerateArray())
        {
            if (!valuesBlock.TryGetProperty("value", out var valueArray) || valueArray.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var valueNode in valueArray.EnumerateArray())
            {
                if (!valueNode.TryGetProperty("value", out var numericNode) ||
                    !valueNode.TryGetProperty("dateTime", out var dateTimeNode))
                {
                    continue;
                }

                var numericText = numericNode.GetString();
                var dateTimeText = dateTimeNode.GetString();
                if (string.IsNullOrWhiteSpace(numericText) || string.IsNullOrWhiteSpace(dateTimeText))
                    continue;

                if (!double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
                    continue;

                if (!DateTimeOffset.TryParse(dateTimeText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var observedAt))
                    continue;

                var parsed = new ParsedMeasurement(observedAt.UtcDateTime, numericValue);
                if (latest == null || parsed.ObservedAtUtc > latest.Value.ObservedAtUtc)
                    latest = parsed;
            }
        }

        return latest;
    }

    private sealed class GaugeReadingBuilder
    {
        public Guid GaugeId { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string GaugeName { get; set; } = string.Empty;
        public DateTime? ObservedAtUtc { get; set; }
        public double? GaugeHeightFeet { get; set; }
        public double? FlowRateCfs { get; set; }
    }

    private readonly record struct ParsedMeasurement(DateTime ObservedAtUtc, double NumericValue);
}
