using Core.Models;
using MediatR;

namespace Core.Commands;

public class UpdateDuplicatesCommand: IRequest<string>
{
    public string TableName { get; set; }
    public List<Dictionary<string, object>> Duplicates { get; set; }
    public List<ColumnInfo> ColumnInfoList { get; set; }

    public UpdateDuplicatesCommand(string tableName, List<Dictionary<string, object>> duplicates, List<ColumnInfo> columnInfoList)
    {
        TableName = tableName;
        Duplicates = duplicates;
        ColumnInfoList = columnInfoList;
    }
}