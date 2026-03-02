using System.Text.Json;
using gradyn_api_2.Services.BLL;
using Microsoft.AspNetCore.Mvc;

namespace gradyn_api_2.Controllers;

/// <summary>
///  Generic controller for form apps
/// </summary>
[ApiController]
[Route("form")]
public sealed class GenericFormController(IGenericFormService genericFormService) : ControllerBase
{
    [HttpPost("{formKey}")]
    public async Task<IActionResult> Submit(
        [FromRoute] string formKey,
        [FromBody] Dictionary<string, JsonElement>? fields,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(formKey))
            return BadRequest("Missing form key.");

        if (fields is null || fields.Count == 0)
            return BadRequest("Missing fields.");

        // Convert JsonElement to string? in a predictable way.
        var normalized = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (k, v) in fields)
        {
            if (string.IsNullOrWhiteSpace(k))
                continue;

            normalized[k] = v.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => v.ToString(),
            };
        }

        if (normalized.Count == 0)
            return BadRequest("No usable fields.");

        await genericFormService.SubmitAsync(formKey, normalized, ct);
        return Ok(new { ok = true });
    }
}