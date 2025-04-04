using Core.Queries;
using Core.Results;
using MediatR;

namespace Core.Handlers;

public class RefreshTokenQueryHandler : IRequestHandler<RefreshTokenQuery, LoginResponse>
{
    private readonly IAuthService _authService;

    public RefreshTokenQueryHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<LoginResponse> Handle(RefreshTokenQuery request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request.Token, request.RefreshToken);

        if (!result.Successful)
        {
            await _authService.LogoutAsync(request.Token);
        }

        return result;
    }
}