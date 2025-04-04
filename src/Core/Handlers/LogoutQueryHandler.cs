using Core.Queries;
using MediatR;

namespace Core.Handlers;

public class LogoutQueryHandler: IRequestHandler<LogoutQuery, bool>
{
    private readonly IAuthService _authService;

    public LogoutQueryHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<bool> Handle(LogoutQuery request, CancellationToken cancellationToken)
    {
        return await _authService.LogoutAsync(request.token);
    }
}