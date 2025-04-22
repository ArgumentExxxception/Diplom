using MediatR;

namespace Core.Queries;

public record GetExportDataSizeQuery(string TableName, string FilterCondition = null) : IRequest<long>;