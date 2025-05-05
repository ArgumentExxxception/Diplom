using Core.DTOs;
using Core.Queries;
using MediatR;

namespace Core.Handlers;

public class GetUserFromTokenQueryHandler : IRequestHandler<GetUserFromTokenQuery, UserDto>
{
    private readonly IAuthService _authService;
    
    public GetUserFromTokenQueryHandler(IAuthService authService)
    {
        _authService = authService;
    }
    
    public async Task<UserDto> Handle(GetUserFromTokenQuery request, CancellationToken cancellationToken)
    {
        return await _authService.GetUserFromTokenAsync(request.Token);
    }
}