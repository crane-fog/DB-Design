using Backend.Services;

using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

[ApiController]
[Route("/")]
public class DefaultApiController(IUserTestService userTestService) : ControllerBase
{
    [HttpGet]
    [Route("get-status")]
    public IActionResult GetStatus()
    {
        var response = new GetStatus200Response
        {
            Status = "ok",
            Uptime = Environment.TickCount64 > int.MaxValue ? int.MaxValue : (int)Environment.TickCount64
        };
        return Ok(response);
    }

    [HttpGet]
    [Route("user-test")]
    public IActionResult GetUserTest()
    {
        var rows = userTestService.GetLatestUsers();
        return Ok(rows);
    }
}
