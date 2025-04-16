using Core.Models;
using MediatR;

namespace Core.Queries;

public class GetTablesMetadataQuery: IRequest<List<ImportColumnsMetadataModel>>
{
    
}