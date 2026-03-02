using Microsoft.AspNetCore.Mvc;

namespace gradyn_api_2.Controllers;

/// <summary>
/// Controller for the root route. Obviously this is for my personal projects and this should not be hit,
/// so the emoji is fine as a little easter egg
/// </summary>
[ApiController]
[Route("/")]
public class RootController : ControllerBase
{
    public IActionResult Index()
    {
        return Ok("Ok 👍");
    }
}