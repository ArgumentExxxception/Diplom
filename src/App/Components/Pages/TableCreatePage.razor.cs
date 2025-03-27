using App.Interfaces;
using Core.Enums;
using Core.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Pages;

public partial class TableCreatePage : ComponentBase
{
    private string _tableName = string.Empty;
    private string _newColumnName = string.Empty;
    private int _newColumnType = 0;
    private bool _newColumnIsPrimaryKey = false;
    private bool _newColumnIsRequired = true;
    private List<ColumnInfo> columns = new();
    
    [Inject] private IDatabaseClientService _databaseClientService { get; set; }

    private void AddColumn()
    {
        if (string.IsNullOrWhiteSpace(_newColumnName))
        {
            Snackbar.Add("Введите название колонки.", Severity.Warning);
            return;
        }

        columns.Add(new ColumnInfo
        {
            Name = _newColumnName,
            Type = _newColumnType,
            IsRequired = _newColumnIsRequired,
            IsPrimaryKey = _newColumnIsPrimaryKey
        });
        
        _newColumnName = string.Empty;
        _newColumnType = 0;
        _newColumnIsPrimaryKey = false;
        _newColumnIsRequired = true;
    }
    
    private void RemoveColumn(ColumnInfo column)
    {
        columns.Remove(column);
    }

    private async Task CreateTable()
    {
        if (string.IsNullOrWhiteSpace(_tableName))
        {
            Snackbar.Add("Введите название таблицы.", Severity.Warning);
            return;
        }

        if (columns.Count == 0)
        {
            Snackbar.Add("Добавьте хотя бы одну колонку.", Severity.Warning);
            return;
        }

        var tableData = new TableModel
        {
            TableName = _tableName,
            Columns = columns,
            PrimaryKey = columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name ?? columns[0].Name,
            TableData = new List<string>()
        };

        try
        {
            var result = await _databaseClientService.CreateTablesAsync(tableData);
            Snackbar.Add("Таблица успешно создана!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Ошибка: {ex.Message}", Severity.Error);
        }
        finally
        {
            _newColumnName = string.Empty;
            _newColumnType = 0;
            _newColumnIsPrimaryKey = false;
            _newColumnIsRequired = true;
        }
    }
}