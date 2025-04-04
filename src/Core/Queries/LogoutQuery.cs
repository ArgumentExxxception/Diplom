using MediatR;

namespace Core.Queries;

public record LogoutQuery(string token): IRequest<bool>;