using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

[ApiController]
[Route("/api")]
public class DefaultApiController(
    IUserTestService userTestService,
    AuthService authService) : ControllerBase
{
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    [Route("login")]
    public IActionResult Login([FromForm] string? userNo, [FromForm(Name = "password")] string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(userNo) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return BadRequest(new LoginPost200Response
            {
                Msg = "请输入账号和密码"
            });
        }

        var user = authService.Authenticate(userNo, passwordHash);
        if (user is null)
        {
            return Unauthorized(new LoginPost200Response
            {
                Msg = "账号或密码错误"
            });
        }

        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var response = new LoginPost200Response
        {
            Msg = "登录成功",
            AccessToken = authService.CreateToken(user.EmployeeNo, expiresAt),
            Expires = expiresAt.ToUnixTimeSeconds()
        };

        return Ok(response);
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    [Route("register")]
    public IActionResult Register(
        [FromForm] string? userNo,
        [FromForm(Name = "password")] string? passwordHash,
        [FromForm] string? userName,
        [FromForm] string? phone,
        [FromForm] string? email)
    {
        if (string.IsNullOrWhiteSpace(userNo)
            || string.IsNullOrWhiteSpace(passwordHash)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new RegisterPost200Response
            {
                Msg = "请填写必填项"
            });
        }

        var errorMessage = authService.Register(
            userNo.Trim(),
            passwordHash,
            userName.Trim(),
            phone.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim());

        if (errorMessage is not null)
        {
            return Conflict(new RegisterPost200Response
            {
                Msg = errorMessage
            });
        }

        return Ok(new RegisterPost200Response
        {
            Msg = "注册成功"
        });
    }

    [HttpGet]
    [Authorize]
    [Route("user-test")]
    public IActionResult GetUserTest()
    {
        var rows = userTestService.GetLatestUsers();
        return Ok(rows);
    }
}
