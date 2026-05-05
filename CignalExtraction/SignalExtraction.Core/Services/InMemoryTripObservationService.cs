using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class InMemoryTripObservationService : ITripObservationService
{
    private readonly List<TripObservation> _observations = new();
    private readonly object _gate = new();

    public TripObservation AddObservation(TripObservation observation)
    {
        lock (_gate)
        {
            _observations.Add(observation);
        }

        return observation;
    }
}
