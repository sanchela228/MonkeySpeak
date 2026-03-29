using Core.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Core.Http.Controllers;

public class SettingsController(IOptions<BackendSettings> settings) : ControllerBase
{
    [HttpGet]
    [Route("/api/settings")]
    public IActionResult Get()
    {
        var s = settings.Value;
        return Ok(new
        {
            authMode = s.AuthMode.ToString()
        });
    }
}
