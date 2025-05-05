using Core.DTOs;
using MediatR;

namespace Core.Queries;

public record GetUserFromTokenQuery(string Token) : IRequest<UserDto>;