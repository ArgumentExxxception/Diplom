using Core.Models;
using MediatR;

namespace Core.Commands;

public record EnqueueImportCommand(
    string FileName,
    long FileSize,
    string ContentType,
    TableImportRequestModel Request,
    Stream FileStream,
    string UserEmail
) : IRequest<BackgroundTask>;