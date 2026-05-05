using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/access-point-identifiers")]
public class AccessPointIdentifiersController : ControllerBase
{
    private readonly IAccessPointIdentifierService _accessPointIdentifierService;

    public AccessPointIdentifiersController(IAccessPointIdentifierService accessPointIdentifierService)
    {
        _accessPointIdentifierService = accessPointIdentifierService;
    }

    [HttpGet]
    public ActionResult<List<AccessPointIdentifierResponse>> GetAccessPointIdentifiers()
    {
        var identifiers = _accessPointIdentifierService
            .GetAccessPointIdentifiers()
            .OrderBy(identifier => identifier.PossibleAccessKey)
            .ThenBy(identifier => identifier.SourceName)
            .ThenBy(identifier => identifier.IdentifierType)
            .Select(identifier => new AccessPointIdentifierResponse(
                identifier.Id,
                identifier.RawAccessPointCandidateId,
                identifier.CatalogAccessPointId,
                identifier.PossibleAccessKey,
                identifier.IdentifierType,
                identifier.IdentifierValue,
                identifier.SourceName,
                identifier.SourceReference,
                identifier.Note,
                identifier.Status))
            .ToList();

        return Ok(identifiers);
    }
}

public record AccessPointIdentifierResponse(
    Guid Id,
    Guid? RawAccessPointCandidateId,
    Guid? CatalogAccessPointId,
    string PossibleAccessKey,
    AccessPointIdentifierType IdentifierType,
    string IdentifierValue,
    string SourceName,
    string? SourceReference,
    string? Note,
    AccessPointIdentifierStatus Status);
