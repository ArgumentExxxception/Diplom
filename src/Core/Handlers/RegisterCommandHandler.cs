using Core.Commands;
using Core.DTOs;
using Core.Results;
using MediatR;

namespace Core.Handlers;

public class RegisterCommandHandler: IRequestHandler<RegisterCommand, LoginResponse>
{
    private readonly IAuthService _authService;

    public RegisterCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<LoginResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        return await _authService.RegisterAsync(request.requestDto);
    }
}