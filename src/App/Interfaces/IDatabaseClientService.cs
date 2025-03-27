using Core.Models;

namespace App.Interfaces;

public interface IDatabaseClientService
{
    Task<List<TableModel>> GetPublicTablesAsync();
    Task<string> CreateTablesAsync(TableModel tableModel);
}