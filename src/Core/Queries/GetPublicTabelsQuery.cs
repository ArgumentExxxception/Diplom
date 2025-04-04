using Core.Models;
using MediatR;

namespace Core.Queries;

public record GetPublicTablesQuery: IRequest<List<TableModel>>
{
    
}