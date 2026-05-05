namespace ProjectCignal.Services;

public class TripEstimatorService
{
    public TripEstimateResult Estimate(
        string segmentName,
        double distanceMiles,
        double paddlingSpeedMph,
        double riverCurrentMph,
        DateTime? launchTime)
    {
        double effectiveSpeed = paddlingSpeedMph + riverCurrentMph;

        if (effectiveSpeed <= 0)
        {
            throw new ArgumentException("Effective speed must be greater than zero.");
        }

        double hours = distanceMiles / effectiveSpeed;
        TimeSpan duration = TimeSpan.FromHours(hours);

        DateTime? finishTime = null;
        if (launchTime.HasValue)
        {
            finishTime = launchTime.Value.Add(duration);
        }

        return new TripEstimateResult
        {
            SegmentName = segmentName,
            EstimatedDuration = duration,
            EstimatedFinishTime = finishTime,
            Assumptions = "This first slice models water movement primarily through river current."
        };
    }

    public string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours} hr {duration.Minutes} min";
    }
    
    public List<RiverSegment> GetPresetSegments()
{
    return new List<RiverSegment>
    {
        new RiverSegment { Name = "Neuse - Anderson Point to Poole Road", DistanceMiles = 7.0, DefaultRiverCurrentMph = 1.5 },
        new RiverSegment { Name = "Cape Fear - Lillington to Erwin", DistanceMiles = 10.0, DefaultRiverCurrentMph = 2.0 },
        new RiverSegment { Name = "Lumber - Boardman to Fair Bluff", DistanceMiles = 12.0, DefaultRiverCurrentMph = 1.2 }
        };
    }
}

public class TripEstimateResult
{
    public string SegmentName { get; set; } = string.Empty;
    public TimeSpan EstimatedDuration { get; set; }
    public DateTime? EstimatedFinishTime { get; set; }
    public string Assumptions { get; set; } = string.Empty;
}

public class RiverSegment
{
    public string Name { get; set; } = string.Empty;
    public double DistanceMiles { get; set; }
    public double DefaultRiverCurrentMph { get; set; }  // Default current speed, can be adjusted as needed
}

