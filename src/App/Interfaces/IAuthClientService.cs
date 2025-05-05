using Core.DTOs;
using Core.Entities;
using Core.Results;

namespace App.Interfaces;

public interface IAuthClientService
{
    Task<LoginResponse> Login(LoginRequestDto loginRequest);
    Task<LoginResponse> Register(RegisterRequestDto registerRequest);
    Task Logout();
    Task Test();
}