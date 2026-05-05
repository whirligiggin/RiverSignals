using Microsoft.AspNetCore.Mvc;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Api.Controllers;

[ApiController]
[Route("extract")]
public class ExtractController : ControllerBase
{
    private readonly IExtractionService _extractionService;

    public ExtractController(IExtractionService extractionService)
    {
        _extractionService = extractionService;
    }

    [HttpPost]
    public async Task<ActionResult<ExtractionResult>> Post([FromBody] ExtractionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text is required.");

        var result = await _extractionService.ExtractAsync(request);

        return Ok(result);
    }
}