using Core.Entities;
using Core.Queries;
using Core.Results;
using MediatR;

namespace Core.Handlers;

public class LoginQueryHandler: IRequestHandler<LoginQuery, LoginResponse>
{
    private readonly IAuthService _authService;

    public LoginQueryHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<LoginResponse> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        LoginRequestDto loginDto = new LoginRequestDto(){ Username = request.username, Password = request.password, RememberMe = request.rememberMe};
        return await _authService.LoginAsync(loginDto);
    }
}