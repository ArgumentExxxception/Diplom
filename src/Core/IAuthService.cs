using Core.DTOs;
using Core.Entities;
using Core.Results;

namespace Core;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequestDto loginRequest);
    Task<LoginResponse> RegisterAsync(RegisterRequestDto registerRequest);
    Task<LoginResponse> RefreshTokenAsync(string token, string refreshToken);
    Task<bool> LogoutAsync(string token);
    Task<bool> ValidateTokenAsync(string token);
}