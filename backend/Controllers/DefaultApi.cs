using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

[ApiController]
[Route("/api")]
public class DefaultApiController(
    IUserTestService userTestService,
    AuthService authService,
    AuthorizationService authorization) : ControllerBase
{
    private const int TokenExpiresSeconds = 7200;

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("login")]
    public IActionResult Login([FromBody] LoginRequest? request)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.EmployeeNo)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return Ok(new LoginResponse
            {
                Code = LoginResponse.CodeEnum._400Enum,
                Message = "请输入账号和密码",
                Data = null!
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = authService.Authenticate(request.EmployeeNo.Trim(), request.Password, ipAddress);
        if (user is null)
        {
            return Ok(new LoginResponse
            {
                Code = LoginResponse.CodeEnum._400Enum,
                Message = "工号或密码错误",
                Data = null!
            });
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(TokenExpiresSeconds);
        var response = new LoginResponse
        {
            Code = LoginResponse.CodeEnum._200Enum,
            Message = "登录成功",
            Data = new LoginData
            {
                AccessToken = authService.CreateToken(user.EmployeeNo, expiresAt),
                Expires = TokenExpiresSeconds
            }
        };

        return Ok(response);
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("register")]
    public IActionResult Register([FromBody] RegisterRequest? request)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.EmployeeNo)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.UserName)
            || string.IsNullOrWhiteSpace(request.Phone))
        {
            return Ok(new RegisterResponse
            {
                Code = RegisterResponse.CodeEnum._400Enum,
                Message = "请填写必填项",
                Data = null!
            });
        }

        var result = authService.Register(
            request.EmployeeNo.Trim(),
            request.Password,
            request.UserName.Trim(),
            request.Phone.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim());

        if (result.ErrorMessage is not null)
        {
            return Ok(new RegisterResponse
            {
                Code = RegisterResponse.CodeEnum._409Enum,
                Message = result.ErrorMessage,
                Data = null!
            });
        }

        var registeredUser = result.User!;
        return Ok(new RegisterResponse
        {
            Code = RegisterResponse.CodeEnum._200Enum,
            Message = "注册成功",
            Data = new UserBrief
            {
                UserId = Convert.ToInt32(registeredUser.UserId),
                EmployeeNo = registeredUser.EmployeeNo,
                UserName = registeredUser.UserName,
                Phone = registeredUser.Phone,
                Email = registeredUser.Email,
                Status = UserBrief.StatusEnum.ValidEnum
            }
        });
    }

    [HttpGet]
    [Authorize]
    [Route("user-test")]
    public IActionResult GetUserTest()
    {
        AuthResult result = authorization.RequireLogin(User.GetEmployeeNo());
        if (!result.Ok)
        {
            return Ok(result.ToApiResponse());
        }

        var rows = userTestService.GetLatestUsers();
        return Ok(rows);
    }
}
