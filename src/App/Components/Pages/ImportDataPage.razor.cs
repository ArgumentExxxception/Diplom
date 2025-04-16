using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using App.Components.Dialogs;
using App.Interfaces;
using Blazored.LocalStorage;
using Core;
using Core.Models;
using Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace App.Components.Pages;

public partial class ImportDataPage : ComponentBase
{
    [Inject] private HttpClient _http { get; set; }
    [Inject] private ISnackbar _snackbar { get; set; }
    [Inject] private IDialogService _dialogService { get; set; }
    [Inject] private IDatabaseClientService _databaseService { get; set; }
    [Inject] private IDataImportClientService _dataImportClientService { get; set; }
    [Inject] private ISnackbar Snackbar { get; set; }
    [Inject] private IBackgroundTaskService _backgroundTaskService { get; set; }
    [Inject] private AuthenticationStateProvider _authStateProvider { get; set; }
    [Inject] private ILocalStorageService _localStorage { get; set; }
    
    private const long BACKGROUND_PROCESSING_THRESHOLD = 25 * 1024 * 1024;
    
    private MudTabs _tabs;
    private List<TableModel> _tabels = new();
    private bool isExpanded = false; // По умолчанию панель свернута
    private int _activePanelIndex = 0;
    
    //переменные в новую таблицу
    private string _tableName = string.Empty;
    private string _newColumnName = string.Empty;
    private int _newColumnType = 0;
    private bool _newColumnIsPrimaryKey = false;
    private bool _newColumnIsRequired = true;
    private bool _newColumnSearchInDuplicates = false;
    private bool _NewColumnIsGeoTag = false;
    private List<ColumnInfo> columns = new();
    private bool isNewTable = false;
    private string _newTableComment = string.Empty;
    
    private string xmlRootElement = string.Empty;
    private int csvSkippedRows = 0;
    private string xmlRowElement = string.Empty;

    private List<string> availableTables = [];
    private List<ColumnInfo> tableStructure = [];
    private string selectedTable;
    private int importMode = 1;
    private IBrowserFile selectedFile;
    private bool isImporting = false;
    private ImportResult importResult;
    private string? _currentUserEmail = string.Empty;
    private string? _selectedTableComment = string.Empty;

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
            _tabels = await _databaseService.GetPublicTablesAsync();
            availableTables = _tabels.Select(tableModel => tableModel.TableName).ToList();
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
            var tableModel = _tabels
                .Where(tableModel => tableModel.TableName == tableName).FirstOrDefault();
            if (tableModel != null)
            {
                tableStructure = tableModel.Columns.ToList();
                _selectedTableComment = tableModel.TableComment;
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Ошибка при загрузке структуры таблицы: {ex.Message}", Severity.Error);
        }
    }

    private void OnInputFileChanged(InputFileChangeEventArgs e)
    {
        selectedFile = e.File;
    }
    
    private void SetActiveTab(int index)
    {
        _activePanelIndex = index;
        isNewTable = index == 1;
        StateHasChanged(); // Обновляем состояние кнопки
    }

    private bool CanStartImport()
    {
        return _activePanelIndex switch
        {
            0 => !string.IsNullOrEmpty(selectedTable) && selectedFile?.Size > 0 && !isImporting,
            1 => !string.IsNullOrEmpty(_tableName) && columns.Count > 0 && selectedFile?.Size > 0,
            _ => false
        };
    }

    private async Task StartImport()
    {
        if (!CanStartImport())
            return;
    
        var parameters = new DialogParameters
        {
            { "ContentText", "Вы уверены, что хотите начать импорт данных?" },
            { "ButtonText", "Да, начать импорт" },
            { "Color", Color.Primary }
        };
    
        var dialog = _dialogService.Show<ConfirmDialog>("Подтверждение", parameters);
        var result = await dialog.Result;
    
        if (result.Canceled)
            return;
        try
        {
            isImporting = true;
            StateHasChanged();
            
            TableImportRequestModel importRequest = BuildImportRequestModel();
            
            importResult = await _dataImportClientService.ImportData(selectedFile,importRequest);
            if (importResult.Success)
            {
                if (importResult.Success)
                {
                    _snackbar.Add("Импорт данных успешно завершен", Severity.Success);
                    if (importResult.DuplicatedRows is { Count: > 0 })
                    {
                        await HandleDuplicateRows(importRequest.TableName,importResult.DuplicatedRows,importRequest.Columns);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            isImporting = false;
            StateHasChanged();
        }
    }
    
    private TableImportRequestModel BuildImportRequestModel()
    {
        if (_activePanelIndex == 0)
        {
            // Import to existing table
            return new TableImportRequestModel
            {
                TableName = selectedTable,
                Columns = tableStructure,
                ImportMode = importMode,
                IsNewTable = isNewTable,
                XmlRootElement = xmlRootElement,
                XmlRowElement = xmlRowElement,
                HasHeaderRow = true,
                UserEmail = _currentUserEmail,
                SkipRows = csvSkippedRows
            };
        }
        else
        {
            // Import to new table
            if (string.IsNullOrWhiteSpace(_tableName))
            {
                Snackbar.Add("Введите название таблицы.", Severity.Warning);
                throw new Exception("Table name is required");
            }

            if (columns.Count == 0)
            {
                Snackbar.Add("Добавьте хотя бы одну колонку.", Severity.Warning);
                throw new Exception("At least one column is required");
            }
            
            return new TableImportRequestModel
            {
                TableName = _tableName,
                Columns = columns,
                ImportMode = 1,
                IsNewTable = isNewTable,
                XmlRootElement = xmlRootElement,
                XmlRowElement = xmlRowElement,
                HasHeaderRow = true,
                UserEmail = _currentUserEmail,
                TableComment = _newTableComment,
                SkipRows = csvSkippedRows
            };
        }
    }

    private async Task HandleDuplicateRows(string tableName,List<Dictionary<string, object>> duplicateRows, List<ColumnInfo> columns)
    {
        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            BackdropClick = true
        };

        var parameters = new DialogParameters
        {
            { nameof(DuplicateDialog.Duplicates), duplicateRows }
        };

        var dialog = await _dialogService.ShowAsync<DuplicateDialog>("Обнаружены дубликаты", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await _dataImportClientService.UpdateDuplicate(tableName, duplicateRows, columns);
        }
    }

    private void OnExpandedChanged(bool expanded)
    {
        isExpanded = expanded; // Обновляем состояние при изменении
    }
    
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
            IsPrimaryKey = _newColumnIsPrimaryKey,
            SearchInDuplicates = _newColumnSearchInDuplicates,
            IsGeoTag = _NewColumnIsGeoTag
        });
        
        _newColumnName = string.Empty;
        _newColumnType = 0;
        _newColumnIsPrimaryKey = false;
        _newColumnIsRequired = false;
        _newColumnSearchInDuplicates = false;
        _NewColumnIsGeoTag = false;
    }
    
    private void RemoveColumn(ColumnInfo column)
    {
        columns.Remove(column);
    }
    private bool IsXMLFile()
    {
        return selectedFile != null && (selectedFile.ContentType.Contains("xml") || 
                selectedFile.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
    }
    private bool IsCsvFile()
    {
        return selectedFile != null && (selectedFile.ContentType.Contains("csv") || 
                                        selectedFile.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
    }
    

    private void ResetForm()
    {
        selectedFile = null;
        importResult = null;
        _newColumnName = string.Empty;
        _newColumnType = 0;
        _newColumnIsPrimaryKey = false;
        _newColumnIsRequired = false;
        _tableName = string.Empty;
        columns.Clear();
        StateHasChanged();
    }
}