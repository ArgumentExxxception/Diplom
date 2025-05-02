using Core.Models;

namespace Core.ServiceInterfaces;

public interface IDatabaseService
{
    Task<List<TableModel>> GetPublicTablesAsync();
    Task CreateTableAsync(TableModel tableModel);
    Task<TableModel?> GetTableAsync(string tableName);
    Task<List<ColumnInfo>> GetColumnInfoAsync(string tableName);
}