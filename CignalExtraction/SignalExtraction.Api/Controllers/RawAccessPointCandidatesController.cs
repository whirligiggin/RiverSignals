using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("api/raw-access-point-candidates")]
public class RawAccessPointCandidatesController : ControllerBase
{
    private readonly IRawAccessPointCandidateService _rawAccessPointCandidateService;

    public RawAccessPointCandidatesController(IRawAccessPointCandidateService rawAccessPointCandidateService)
    {
        _rawAccessPointCandidateService = rawAccessPointCandidateService;
    }

    [HttpGet]
    public ActionResult<List<RawAccessPointCandidateResponse>> GetRawAccessPointCandidates()
    {
        var candidates = _rawAccessPointCandidateService
            .GetRawAccessPointCandidates()
            .OrderBy(candidate => candidate.SourceName)
            .ThenBy(candidate => candidate.Name)
            .Select(candidate => new RawAccessPointCandidateResponse(
                candidate.Id,
                candidate.Name,
                candidate.Address,
                candidate.Latitude,
                candidate.Longitude,
                candidate.RiverName,
                candidate.DescriptiveClues,
                candidate.SourceType,
                candidate.SourceName,
                candidate.SourceUrl,
                candidate.CapturedAtUtc,
                candidate.IsResolved,
                candidate.ReviewState,
                candidate.ReviewerNote))
            .ToList();

        return Ok(candidates);
    }

    [HttpPut("{candidateId:guid}/review")]
    public ActionResult<RawAccessPointCandidateResponse> UpdateReviewState(
        Guid candidateId,
        [FromBody] RawAccessPointCandidateReviewRequest? request)
    {
        if (request == null)
            return BadRequest("Review payload is required.");

        if (!Enum.IsDefined(request.ReviewState))
            return BadRequest("ReviewState is not supported.");

        var candidate = _rawAccessPointCandidateService.UpdateReviewState(
            candidateId,
            request.ReviewState,
            request.ReviewerNote);

        if (candidate == null)
            return NotFound($"No raw access point candidate found for {candidateId}.");

        return Ok(new RawAccessPointCandidateResponse(
            candidate.Id,
            candidate.Name,
            candidate.Address,
            candidate.Latitude,
            candidate.Longitude,
            candidate.RiverName,
            candidate.DescriptiveClues,
            candidate.SourceType,
            candidate.SourceName,
            candidate.SourceUrl,
            candidate.CapturedAtUtc,
            candidate.IsResolved,
            candidate.ReviewState,
            candidate.ReviewerNote));
    }
}

public class RawAccessPointCandidateReviewRequest
{
    public RawAccessPointReviewState ReviewState { get; set; }
    public string? ReviewerNote { get; set; }
}

public record RawAccessPointCandidateResponse(
    Guid Id,
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude,
    string RiverName,
    string? DescriptiveClues,
    RawAccessPointSourceType SourceType,
    string SourceName,
    string SourceUrl,
    DateTime CapturedAtUtc,
    bool IsResolved,
    RawAccessPointReviewState ReviewState,
    string? ReviewerNote);
