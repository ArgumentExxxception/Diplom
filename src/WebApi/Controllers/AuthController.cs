using System.Security.Claims;
using Core.Commands;
using Core.DTOs;
using Core.Entities;
using Core.Exceptions;
using Core.Queries;
using Core.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Login([FromBody] LoginRequestDto loginRequest)
    {
        var result = await _mediator.Send(new LoginQuery(loginRequest.Username, loginRequest.Password, loginRequest.RememberMe));
        
        if (!result.Successful)
        {
            return Unauthorized(ApiResponse<LoginResponse>.Fail(result.Error, StatusCodes.Status401Unauthorized));
        }
        
        SetTokenCookie(result.Token, "AppAccessToken", loginRequest.RememberMe);
        
        SetTokenCookie(result.RefreshToken, "AppRefreshToken", true);
        
        return Ok(ApiResponse<UserDto>.SuccessBuild(result.User, "Вход выполнен успешно"));
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserDto>>> RefreshToken()
    {
        var accessToken = Request.Cookies["AppAccessToken"];
        var refreshToken = Request.Cookies["AppRefreshToken"];
        
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("Токены отсутствуют", StatusCodes.Status401Unauthorized));
        }
        
        var result = await _mediator.Send(new RefreshTokenQuery(accessToken, refreshToken));
        
        if (!result.Successful)
        {
            Response.Cookies.Delete("AppAccessToken");
            Response.Cookies.Delete("AppRefreshToken");
            return Unauthorized(ApiResponse<UserDto>.Fail(result.Error, StatusCodes.Status401Unauthorized));
        }
        
        SetTokenCookie(result.Token, "AppAccessToken", false);
        SetTokenCookie(result.RefreshToken, "AppRefreshToken", true);
        
        return Ok(ApiResponse<UserDto>.SuccessBuild(result.User, "Токен успешно обновлен"));
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Logout()
    {
        var token = Request.Cookies["AppAccessToken"];
        
        if (!string.IsNullOrEmpty(token))
        {
            await _mediator.Send(new LogoutQuery(token));
        }
        
        Response.Cookies.Delete("AppAccessToken");
        Response.Cookies.Delete("AppRefreshToken");
        
        return Ok(ApiResponse.Successed("Выход выполнен успешно"));
    }

    [HttpGet("currentuser")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetCurrentUser()
    {
        var token = Request.Cookies["AppAccessToken"];
        
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("Пользователь не авторизован"));
        }
        
        try
        {
            var userInfo = await _mediator.Send(new GetUserFromTokenQuery(token));
            return Ok(ApiResponse<UserDto>.SuccessBuild(userInfo));
        }
        catch (Exception ex)
        {
            return Unauthorized(ApiResponse<UserDto>.Fail(ex.Message));
        }
    }

    private void SetTokenCookie(string token, string cookieName, bool isPersistent)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = isPersistent 
                ? DateTimeOffset.UtcNow.AddDays(30) 
                : DateTimeOffset.UtcNow.AddHours(24)
        };
        
        Response.Cookies.Append(cookieName, token, cookieOptions);
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Register([FromBody] RegisterRequestDto registerRequest)
    {
        try
        {
            if (registerRequest.Password != registerRequest.ConfirmPassword)
            {
                throw new ValidationException("Пароли не совпадают", 
                    new List<string> { "Пароль и подтверждение пароля должны совпадать" });
            }
            
            var result = await _mediator.Send(new RegisterCommand(registerRequest));
            
            if (!result.Successful)
            {
                return BadRequest(ApiResponse<LoginResponse>.Fail(result.Error, StatusCodes.Status400BadRequest));
            }
            
            return Ok(ApiResponse<LoginResponse>.SuccessBuild(result, "Регистрация выполнена успешно"));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ApiResponse<LoginResponse>.Fail(ex.Message, StatusCodes.Status400BadRequest, ex.Errors));
        }
    }
    
    [HttpGet("whoami")]
    public async Task<string?> WhoAmI()
    {
        if (User.Identity.IsAuthenticated)
        {
            return User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        }
        return "Не авторизован";
    }
}