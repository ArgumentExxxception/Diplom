using App.Components.Dialogs;
using App.Interfaces;
using Core.Enums;
using Core.Models;
using Core.Results;
using Microsoft.AspNetCore.Components;
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
    private List<ColumnInfo> columns = new();
    private bool isNewTable = false;
    
    private string xmlRootElement = string.Empty;
    private string xmlRowElement = string.Empty;

    private List<string> availableTables = [];
    private List<ColumnInfo> tableStructure = [];
    private string selectedTable;
    private int importMode = 1;
    private IBrowserFile selectedFile;
    private bool isImporting = false;
    private ImportResult importResult;

    protected override async Task OnInitializedAsync()
    {
        await LoadAvailableTables();
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
            tableStructure = _tabels
                .Where(tableModel => tableModel.TableName == tableName)
                .SelectMany(table => table.Columns)
                .ToList();
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
    
            // // Загрузка структуры таблицы, если еще не загружена
            // if (tableStructure.Count == 0)
            // {
            //     await LoadTableStructure(selectedTable);
            // }
    
            // Формирование запроса на импорт
            using var content = new MultipartFormDataContent();
            TableImportRequestModel importRequest = new TableImportRequestModel();
            //
            // // Добавление файла
            // using var fileContent = new StreamContent(selectedFile.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024)); // 50 МБ максимум
            // content.Add(fileContent, "file", selectedFile.Name);

            if (_activePanelIndex == 0)
            {
                // Добавление параметров импорта
                importRequest = new TableImportRequestModel
                {
                    TableName = selectedTable,
                    Columns = tableStructure,
                    ImportMode = importMode,
                    IsNewTable = isNewTable,
                    XmlRootElement = xmlRootElement,
                    XmlRowElement = xmlRowElement,
                    HasHeaderRow = true
                };
            }
            else if (_activePanelIndex == 1)
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
                importRequest = new TableImportRequestModel
                {
                    TableName = _tableName,
                    Columns = columns,
                    ImportMode = 1,
                    IsNewTable = isNewTable,
                    XmlRootElement = xmlRootElement,
                    XmlRowElement = xmlRowElement,
                    HasHeaderRow = true
                };
            }


            // Отправка запроса
            importResult = await _dataImportClientService.ImportData(selectedFile,importRequest);
            
            if (importResult.Success)
            {
                if (importResult.Success)
                {
                    _snackbar.Add("Импорт данных успешно завершен", Severity.Success);
                    if (importResult.DuplicatedRows is { Count: > 0 })
                    {
                        await HandleDuplicateRows(importRequest.TableName,importResult.DuplicatedRows);
                    }
                }
            }
            else
            {
                // var errorContent = await response.Content.ReadAsStringAsync();
                // _snackbar.Add($"Ошибка сервера: {errorContent}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Ошибка: {ex.Message}", Severity.Error);
        }
        finally
        {
            isImporting = false;
            StateHasChanged();
        }
    }

    private async Task HandleDuplicateRows(string tableName,List<Dictionary<string, object>> duplicateRows)
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
            await _dataImportClientService.UpdateDuplicate(tableName, duplicateRows);
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
            IsPrimaryKey = _newColumnIsPrimaryKey
        });
        
        _newColumnName = string.Empty;
        _newColumnType = 0;
        _newColumnIsPrimaryKey = false;
        _newColumnIsRequired = false;
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