using App.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Components;

namespace App.Components.Pages;

public partial class AllTablesPage : ComponentBase
{
    [Inject] private IDatabaseClientService _databaseClientService { get; set; }
    private bool _isInitialized = false;
    private string searchString = string.Empty;
    private bool sortByTableNameAsc = true;
    private List<TableModel> tables = new();

    protected override async Task OnInitializedAsync()
    {
        tables = await _databaseClientService.GetPublicTablesAsync();
        _isInitialized = true;
    }
    
    private IEnumerable<TableModel> FilteredTables =>
        tables.Where(t => string.IsNullOrEmpty(searchString) || t.TableName.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => sortByTableNameAsc ? t.TableName : null)
            .ThenByDescending(t => !sortByTableNameAsc ? t.TableName : null);

    private void SortByTableName()
    {
        sortByTableNameAsc = !sortByTableNameAsc;
    }
}