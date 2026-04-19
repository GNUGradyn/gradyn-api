using gradyn_api_2.Services.BLL;
using Microsoft.AspNetCore.Mvc;

namespace gradyn_api_2.Controllers;

[ApiController]
[Route("webhook")]
public class StaticUpdateController(IStaticUpdateService staticUpdateService) : ControllerBase
{
    [HttpGet("{webhookId}")]
    public async Task<IActionResult> PerformStaticUpdate(string webhookId)
    {
        try
        {
            await staticUpdateService.PerformStaticUpdateAsync(webhookId);
        }
        catch (KeyNotFoundException ex)
        {
            return StatusCode(404, ex.Message);
        }
        return Ok();
    }
}