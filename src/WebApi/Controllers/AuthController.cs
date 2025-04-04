using Core.Commands;
using Core.DTOs;
using Core.Entities;
using Core.Queries;
using Core.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequestDto loginRequest)
    {
        var result = await _mediator.Send(new LoginQuery(loginRequest.Username, loginRequest.Password, loginRequest.RememberMe));
        return result.Successful ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerRequest)
    {
        var result = await _mediator.Send(new RegisterCommand(registerRequest));
        return result.Successful ? Ok(result) : BadRequest(result);
    }


    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        await _mediator.Send(new LogoutQuery(token));
        return Ok();
    }
    
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto refreshRequest)
    {
        var result = await _mediator.Send(new RefreshTokenQuery(refreshRequest.Token, refreshRequest.RefreshToken));
        return result.Successful ? Ok(result) : Unauthorized(result);
    }
}