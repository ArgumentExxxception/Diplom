using AutoMapper;
using Core.Models;
using Core.Queries;
using Domain.Entities;
using MediatR;

namespace Core.Handlers;

public class GetTablesMetadataQueryHandler: IRequestHandler<GetTablesMetadataQuery,List<ImportColumnsMetadataModel>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetTablesMetadataQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = _unitOfWork;
        _mapper = mapper;
    }
    public async Task<List<ImportColumnsMetadataModel>> Handle(GetTablesMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.ImportColumnMetadatas.GetAllAsync();
        List<ImportColumnsMetadataModel> importColumnsMetadataModels = new List<ImportColumnsMetadataModel>();
        foreach (var icmm in result)
        {
            importColumnsMetadataModels.Add(_mapper.Map<ImportColumnMetadataEntity, ImportColumnsMetadataModel>(icmm));
        }
        return importColumnsMetadataModels;
    }
}