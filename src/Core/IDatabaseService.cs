using Core.Models;

namespace Core;

public interface IDatabaseService
{
    Task<List<TableModel>> GetPublicTablesAsync();
    Task CreateTableAsync(TableModel tableModel);

    Task<List<ColumnInfo>> GetColumnInfoAsync(string tableName);
}