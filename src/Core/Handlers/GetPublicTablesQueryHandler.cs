using Core.Models;
using Core.Queries;
using Core.ServiceInterfaces;
using MediatR;

namespace Core.Handlers;

public class GetPublicTablesQueryHandler: IRequestHandler<GetPublicTablesQuery,List<TableModel>>
{
    private readonly IDatabaseService _databaseService;

    public GetPublicTablesQueryHandler(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<TableModel>> Handle(GetPublicTablesQuery request, CancellationToken cancellationToken)
    {
        return await _databaseService.GetPublicTablesAsync();
    }
}