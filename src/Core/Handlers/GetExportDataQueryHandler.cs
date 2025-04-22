using Core.Queries;
using Core.RepoInterfaces;
using MediatR;

namespace Core.Handlers;

public class GetExportDataSizeQueryHandler: IRequestHandler<GetExportDataSizeQuery, long>
{
    private readonly IDataExportRepository _dataExportRepository;

    public GetExportDataSizeQueryHandler(IDataExportRepository dataExportRepository)
    {
        _dataExportRepository = dataExportRepository;
    }
    
    public async Task<long> Handle(GetExportDataSizeQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataExportRepository.GetDataForExportAsync(
            request.TableName, 
            null, 
            request.FilterCondition, 
            100,
            cancellationToken);

        if (result.Rows.Count == 0)
            return 0;
        
        int sampleSize = Math.Min(result.Rows.Count, 100);
        long totalSampleBytes = 0;
        
        for (int i = 0; i < sampleSize; i++)
        {
            var row = result.Rows[i];
            long rowSize = row.Sum(kv => 
                (kv.Key?.Length ?? 0) + 
                (kv.Value?.ToString()?.Length ?? 0));
            totalSampleBytes += rowSize;
        }

        long averageRowSize = totalSampleBytes / sampleSize;
        long totalEstimatedSize = averageRowSize * result.Rows.Count;
        
        return totalEstimatedSize;
    }
}