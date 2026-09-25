using Microsoft.AspNetCore.Mvc;
using ParsingEndpoint.Models;
using ParsingEndpoint.Services;

namespace ParsingEndpoint.Controllers;

[ApiController]
[Route("api/parse")]
public sealed class ParsingController(IParsingService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ParsingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ParsingResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ParsingResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Parse([FromBody] ParsingRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ProcessAsync(request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}

