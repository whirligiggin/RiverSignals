using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface ITripObservationService
{
    TripObservation AddObservation(TripObservation observation);
}
