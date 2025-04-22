using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using App.Components.Dialogs;
using App.Interfaces;
using Blazored.LocalStorage;
using Core.Models;
using Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace App.Components.Pages;

public partial class ExportDataPage : ComponentBase
{
    [Inject] private HttpClient _http { get; set; }
    [Inject] private ISnackbar _snackbar { get; set; }
    [Inject] private IDialogService _dialogService { get; set; }
    [Inject] private IDatabaseClientService _databaseService { get; set; }
    [Inject] private IDataExportClientService _dataExportClientService { get; set; }
    [Inject] private AuthenticationStateProvider _authStateProvider { get; set; }
    [Inject] private ILocalStorageService _localStorage { get; set; }
    
    private List<TableModel> _tables = new();
    private bool isExpanded = false;
    
    private string _selectedTableComment = string.Empty;
    
    private string selectedTable;
    private ExportFormat exportFormat = ExportFormat.CSV;
    private string exportFormatCsv = "CSV";
    private string exportFormatXml = "CSV";
    private bool includeHeaders = true;
    private string csvDelimiter = ",";
    private string xmlRootElement = "root";
    private string xmlRowElement = "row";
    private string filterCondition = string.Empty;
    private int maxRows = 0;
    private List<string> selectedColumns = new();
    
    private List<string> availableTables = [];
    private List<ColumnInfo> tableStructure = [];
    private string? _currentUserEmail = string.Empty;
    
    private bool isExporting = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadAvailableTables();
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var token = await _localStorage.GetItemAsync<string>("authToken");
        _currentUserEmail = GetEmailFromToken(token);
    }
    
    private string GetEmailFromToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    }

    private async Task LoadAvailableTables()
    {
        try
        {
            _tables = await _databaseService.GetPublicTablesAsync();
            availableTables = _tables.Select(tableModel => tableModel.TableName).ToList();
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Ошибка при загрузке списка таблиц: {ex.Message}", Severity.Error);
        }
    }

    private async Task LoadTableStructure(string tableName)
    {
        try
        {
            var tableModel = _tables
                .Where(tableModel => tableModel.TableName == tableName).FirstOrDefault();
            if (tableModel != null)
            {
                tableStructure = tableModel.Columns.ToList();
                _selectedTableComment = tableModel.TableComment;
                
                selectedColumns.Clear();
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Ошибка при загрузке структуры таблицы: {ex.Message}", Severity.Error);
        }
    }
    
    private void OnExpandedChanged(bool expanded)
    {
        isExpanded = expanded;
    }
    
    private void ToggleColumnSelection(string columnName, bool isChecked)
    {
        if (isChecked && !selectedColumns.Contains(columnName))
        {
            selectedColumns.Add(columnName);
        }
        else if (!isChecked && selectedColumns.Contains(columnName))
        {
            selectedColumns.Remove(columnName);
        }
    }
    
    private bool CanStartExport()
    {
        return !string.IsNullOrEmpty(selectedTable) && !string.IsNullOrEmpty(exportFormat.ToString());
    }

    private async Task StartExport()
    {
        if (!CanStartExport())
            return;
    
        var parameters = new DialogParameters
        {
            { "ContentText", "Вы уверены, что хотите начать экспорт данных?" },
            { "ButtonText", "Да, начать экспорт" },
            { "Color", Color.Primary }
        };
    
        var dialog = await _dialogService.ShowAsync<ConfirmDialog>("Подтверждение", parameters);
        var result = await dialog.Result;
    
        if (result != null && result.Canceled)
            return;
            
        try
        {
            isExporting = true;
            StateHasChanged();
            
            var exportRequest = new TableExportRequestModel
            {
                TableName = selectedTable,
                ExportFormat = exportFormat.ToString(),
                IncludeHeaders = includeHeaders,
                Delimiter = csvDelimiter,
                XmlRootElement = xmlRootElement,
                XmlRowElement = xmlRowElement,
                FilterCondition = filterCondition,
                MaxRows = maxRows,
                UserEmail = _currentUserEmail,
                Columns = selectedColumns
            };
            
            await _dataExportClientService.ExportDataAsync(exportRequest);
            
            _snackbar.Add("Экспорт данных успешно завершен", Severity.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Ошибка при экспорте: {ex.Message}", Severity.Error);
        }
        finally
        {
            isExporting = false;
            StateHasChanged();
        }
    }
    
    private void ResetForm()
    {
        exportFormat = ExportFormat.CSV;
        includeHeaders = true;
        csvDelimiter = ",";
        xmlRootElement = "root";
        xmlRowElement = "row";
        filterCondition = string.Empty;
        maxRows = 0;
        selectedColumns.Clear();
        
        StateHasChanged();
    }
}
