using Core.Results;
using MediatR;

namespace Core.Queries;

public record RefreshTokenQuery(string Token, string RefreshToken): IRequest<LoginResponse>;