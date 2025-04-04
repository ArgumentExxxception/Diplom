using Core.Entities;
using Core.Results;
using MediatR;

namespace Core.Queries;

public record LoginQuery(string username, string password, bool rememberMe): IRequest<LoginResponse>;