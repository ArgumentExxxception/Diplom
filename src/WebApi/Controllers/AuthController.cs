using Core.Commands;
using Core.DTOs;
using Core.Entities;
using Core.Exceptions;
using Core.Queries;
using Core.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequestDto loginRequest)
    {
        var result = await _mediator.Send(new LoginQuery(loginRequest.Username, loginRequest.Password, loginRequest.RememberMe));
    
        if (!result.Successful)
            return Unauthorized(ApiResponse<LoginResponse>.Fail(result.Error));

        // access_token cookie
        Response.Cookies.Append("access_token", result.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // ⚠️ обязательно для SameSite=None
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddMinutes(15)
        });
        // refresh_token cookie
        Response.Cookies.Append("refresh_token", result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        });

        return Ok(ApiResponse<LoginResponse>.SuccessBuild(result, "Вход выполнен"));
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Register([FromBody] RegisterRequestDto registerRequest)
    {
        try
        {
            // Проверяем пароль и его подтверждение
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

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("access_token");
        Response.Cookies.Delete("refresh_token");
        return Ok(ApiResponse.Successed("Выход выполнен"));
    }
    
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> RefreshToken([FromBody] RefreshTokenRequestDto refreshRequest)
    {
        var result = await _mediator.Send(new RefreshTokenQuery(refreshRequest.Token, refreshRequest.RefreshToken));
        
        if (!result.Successful)
        {
            return Unauthorized(ApiResponse<LoginResponse>.Fail(result.Error, StatusCodes.Status401Unauthorized));
        }
        
        return Ok(ApiResponse<LoginResponse>.SuccessBuild(result, "Токен успешно обновлен"));
    }
    
    [HttpGet("whoami")]
    public IActionResult WhoAmI()
    {
        if (!User.Identity.IsAuthenticated)
            return Unauthorized("Не аутентифицирован");

        return Ok(User.Claims.Select(c => new { c.Type, c.Value }));
    }
}