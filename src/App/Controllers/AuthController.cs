using Core;
using Core.DTOs;
using Core.Entities;
using Core.Results;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace App.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequestDto loginRequest)
    {
        var result = await _authService.LoginAsync(loginRequest);

        if (result.Successful)
            return Ok(result);
            
        return Unauthorized(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerRequest)
    {
        var result = await _authService.RegisterAsync(registerRequest);

        if (result.Successful)
            return Ok(result);
            
        return BadRequest(result);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto refreshRequest)
    {
        var result = await _authService.RefreshTokenAsync(refreshRequest.Token, refreshRequest.RefreshToken);

        if (result.Successful)
            return Ok(result);
            
        return Unauthorized(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
        await _authService.LogoutAsync(token);
            
        return Ok();
    }
}