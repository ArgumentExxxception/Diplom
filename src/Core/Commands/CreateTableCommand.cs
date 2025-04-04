using Core.Models;
using MediatR;

namespace Core.Commands;

public record CreateTableCommand(TableModel tableModel): IRequest<string>;