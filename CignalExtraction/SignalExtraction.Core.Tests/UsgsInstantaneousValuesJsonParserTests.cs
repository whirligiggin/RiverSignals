using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class UsgsInstantaneousValuesJsonParserTests
{
    [Fact]
    public void Parse_ExtractsLatestGaugeHeightAndDischarge_ByStation()
    {
        var gauges = new List<UsgsGauge>
        {
            new() { Id = Guid.NewGuid(), RiverId = Guid.NewGuid(), StationId = "02087183", Name = "Neuse River | Falls Dam (Raleigh)" },
            new() { Id = Guid.NewGuid(), RiverId = Guid.NewGuid(), StationId = "02096500", Name = "Haw River | near Bynum" }
        };

        var parsed = UsgsInstantaneousValuesJsonParser.Parse(
            """
            {
              "value": {
                "timeSeries": [
                  {
                    "sourceInfo": { "siteCode": [ { "value": "02087183" } ] },
                    "variable": { "variableCode": [ { "value": "00060" } ] },
                    "values": [ { "value": [ { "value": "3120", "dateTime": "2026-04-27T13:45:00.000-04:00" } ] } ]
                  },
                  {
                    "sourceInfo": { "siteCode": [ { "value": "02087183" } ] },
                    "variable": { "variableCode": [ { "value": "00065" } ] },
                    "values": [ { "value": [ { "value": "4.82", "dateTime": "2026-04-27T13:45:00.000-04:00" } ] } ]
                  },
                  {
                    "sourceInfo": { "siteCode": [ { "value": "02096500" } ] },
                    "variable": { "variableCode": [ { "value": "00060" } ] },
                    "values": [ { "value": [ { "value": "2550", "dateTime": "2026-04-27T13:30:00.000-04:00" } ] } ]
                  },
                  {
                    "sourceInfo": { "siteCode": [ { "value": "02096500" } ] },
                    "variable": { "variableCode": [ { "value": "00065" } ] },
                    "values": [ { "value": [ { "value": "6.10", "dateTime": "2026-04-27T13:30:00.000-04:00" } ] } ]
                  }
                ]
              }
            }
            """,
            gauges);

        Assert.Equal(2, parsed.Count);

        var neuse = Assert.Single(parsed, reading => reading.StationId == "02087183");
        Assert.Equal(3120, neuse.FlowRateCfs);
        Assert.Equal(4.82, neuse.GaugeHeightFeet);
        Assert.Equal(new DateTime(2026, 4, 27, 17, 45, 0, DateTimeKind.Utc), neuse.ObservedAtUtc);

        var haw = Assert.Single(parsed, reading => reading.StationId == "02096500");
        Assert.Equal(2550, haw.FlowRateCfs);
        Assert.Equal(6.10, haw.GaugeHeightFeet);
        Assert.Equal(new DateTime(2026, 4, 27, 17, 30, 0, DateTimeKind.Utc), haw.ObservedAtUtc);
    }

    [Fact]
    public void Parse_IgnoresUnknownStations_AndMissingMeasurements()
    {
        var gauges = new List<UsgsGauge>
        {
            new() { Id = Guid.NewGuid(), RiverId = Guid.NewGuid(), StationId = "02087183", Name = "Neuse River | Falls Dam (Raleigh)" }
        };

        var parsed = UsgsInstantaneousValuesJsonParser.Parse(
            """
            {
              "value": {
                "timeSeries": [
                  {
                    "sourceInfo": { "siteCode": [ { "value": "99999999" } ] },
                    "variable": { "variableCode": [ { "value": "00060" } ] },
                    "values": [ { "value": [ { "value": "3120", "dateTime": "2026-04-27T13:45:00.000-04:00" } ] } ]
                  },
                  {
                    "sourceInfo": { "siteCode": [ { "value": "02087183" } ] },
                    "variable": { "variableCode": [ { "value": "00060" } ] },
                    "values": [ { "value": [ { "value": "", "dateTime": "2026-04-27T13:45:00.000-04:00" } ] } ]
                  }
                ]
              }
            }
            """,
            gauges);

        Assert.Empty(parsed);
    }
}
