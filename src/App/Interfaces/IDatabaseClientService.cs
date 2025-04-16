using Core.Models;
using Domain.Entities;

namespace App.Interfaces;

public interface IDatabaseClientService
{
    Task<List<TableModel>> GetPublicTablesAsync();
    Task<string> CreateTablesAsync(TableModel tableModel);
    Task<List<ImportColumnsMetadataModel>> GetTablesMetadataAsync();
}