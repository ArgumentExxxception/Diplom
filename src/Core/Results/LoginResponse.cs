using Core.DTOs;

namespace Core.Results;

public class LoginResponse
{
    public bool Successful { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
    public UserDto User { get; set; }
    public string Error { get; set; }
}