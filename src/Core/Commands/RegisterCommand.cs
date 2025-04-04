using Core.DTOs;
using Core.Results;
using MediatR;

namespace Core.Commands;

public record RegisterCommand(RegisterRequestDto requestDto): IRequest<LoginResponse>;