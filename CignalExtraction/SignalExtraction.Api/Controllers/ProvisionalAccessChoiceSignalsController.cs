using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/provisional-access-choice-signals")]
public class ProvisionalAccessChoiceSignalsController : ControllerBase
{
    private readonly IProvisionalAccessChoiceSignalService _choiceSignalService;

    public ProvisionalAccessChoiceSignalsController(IProvisionalAccessChoiceSignalService choiceSignalService)
    {
        _choiceSignalService = choiceSignalService;
    }

    [HttpGet]
    public ActionResult<List<ProvisionalAccessChoiceSignalResponse>> GetProvisionalAccessChoiceSignals()
    {
        var signals = _choiceSignalService
            .GetProvisionalAccessChoiceSignals()
            .OrderBy(signal => signal.PossibleAccessKey)
            .ThenBy(signal => signal.SourceName)
            .ThenBy(signal => signal.SelectedAtUtc)
            .Select(signal => new ProvisionalAccessChoiceSignalResponse(
                signal.Id,
                signal.RawAccessPointCandidateId,
                signal.PossibleAccessKey,
                signal.SignalSource,
                signal.SelectedAtUtc,
                signal.SourceName,
                signal.SourceReference,
                signal.Note))
            .ToList();

        return Ok(signals);
    }
}

public record ProvisionalAccessChoiceSignalResponse(
    Guid Id,
    Guid? RawAccessPointCandidateId,
    string? PossibleAccessKey,
    ProvisionalAccessChoiceSignalSource SignalSource,
    DateTime SelectedAtUtc,
    string SourceName,
    string? SourceReference,
    string? Note);
