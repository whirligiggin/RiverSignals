using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/provisional-access-pairs")]
public class ProvisionalAccessPairsController : ControllerBase
{
    private readonly IProvisionalAccessPairService _pairService;

    public ProvisionalAccessPairsController(IProvisionalAccessPairService pairService)
    {
        _pairService = pairService;
    }

    [HttpGet]
    public ActionResult<List<ProvisionalAccessPairResponse>> GetProvisionalAccessPairs()
    {
        var pairs = _pairService
            .GetProvisionalAccessPairs()
            .OrderBy(pair => pair.SourceName)
            .ThenBy(pair => pair.CreatedAtUtc)
            .Select(pair => new ProvisionalAccessPairResponse(
                pair.Id,
                ToResponse(pair.PutIn),
                ToResponse(pair.TakeOut),
                pair.DistanceMiles,
                pair.DistanceBasis,
                pair.DistanceSourceName,
                pair.DistanceSourceReference,
                pair.CreatedAtUtc,
                pair.SourceName,
                pair.SourceReference,
                pair.Note))
            .ToList();

        return Ok(pairs);
    }

    private static ProvisionalAccessReferenceResponse ToResponse(ProvisionalAccessReference reference)
    {
        return new ProvisionalAccessReferenceResponse(
            reference.RawAccessPointCandidateId,
            reference.PossibleAccessKey,
            reference.Basis,
            reference.Label);
    }
}

public record ProvisionalAccessPairResponse(
    Guid Id,
    ProvisionalAccessReferenceResponse PutIn,
    ProvisionalAccessReferenceResponse TakeOut,
    double? DistanceMiles,
    ProvisionalAccessPairDistanceBasis DistanceBasis,
    string? DistanceSourceName,
    string? DistanceSourceReference,
    DateTime CreatedAtUtc,
    string SourceName,
    string? SourceReference,
    string? Note);

public record ProvisionalAccessReferenceResponse(
    Guid? RawAccessPointCandidateId,
    string? PossibleAccessKey,
    ProvisionalAccessReferenceBasis Basis,
    string? Label);
