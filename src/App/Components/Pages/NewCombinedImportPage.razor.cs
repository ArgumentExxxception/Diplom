using App.Components.Dialogs;
using App.Interfaces;
using Core.Enums;
using Core.Models;
using Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using Core.Errors;

namespace App.Components.Pages;

public partial class NewCombinedImportPage : ComponentBase
{
    [Inject] private HttpClient _http { get; set; }
    [Inject] private ISnackbar _snackbar { get; set; }
    [Inject] private IDialogService _dialogService { get; set; }
    [Inject] private IDatabaseClientService _databaseService { get; set; }
    [Inject] private IDataImportClientService _dataImportClientService { get; set; }

    private MudTabs _tabs;
    private int _activeTabIndex = 0;

    // Общие поля для импорта
    private IBrowserFile selectedFile;
    private bool isImporting = false;
    private ImportResult importResult;
    private string csvDelimiter = ",";
    private bool hasHeaderRow = true;
    private string fileEncoding = "UTF-8";
    private string xmlRootElement = "";
    private string xmlRowElement = "row";
    private int _dataImportMode = 1;

    // Поля для импорта в существующую таблицу
    private List<string> availableTables = [];
    private List<ColumnInfo> tableStructure = [];
    private List<ColumnInfo> tableStructureModel = [];
    private string selectedTable;
    private bool showColumnMapping = true;

    // Поля для создания новой таблицы
    private string newTableName = string.Empty;
    private string newColumnName = string.Empty;
    private ColumnTypes newColumnType = ColumnTypes.Text;
    private bool newColumnIsPrimaryKey = false;
    private bool newColumnIsRequired = true;
    private List<ColumnInfo> columns = new();
    
    List<TableModel> _tables = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadAvailableTables();
    }

    private void SetActiveTab(int index)
    {
        _activeTabIndex = index;
        // Можно добавить дополнительную логику при переключении вкладок
        StateHasChanged();
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
        if (string.IsNullOrEmpty(tableName))
            return;
            
        try
        {
            tableStructure = _tables.Where(table => table.TableName == tableName)
                .SelectMany(table => table.Columns).ToList();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Ошибка при загрузке структуры таблицы: {ex.Message}", Severity.Error);
        }
    }

    private void OnInputFileChanged(InputFileChangeEventArgs e)
    {
        selectedFile = e.File;
        // Автоматически определяем тип файла и устанавливаем соответствующие параметры
        if (IsCSVFile())
        {
            csvDelimiter = ",";
            hasHeaderRow = true;
        }
        else if (IsXMLFile())
        {
            xmlRootElement = ""; // Будет определено автоматически
            xmlRowElement = "row"; // По умолчанию
        }
    }
    
    private bool IsCSVFile()
    {
        return selectedFile != null && 
               (selectedFile.ContentType.Contains("csv") || 
                selectedFile.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
    }
    
    private bool IsXMLFile()
    {
        return (selectedFile.ContentType.Contains("xml") || 
                selectedFile.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
    }

    private bool CanStartImport()
    {
        // Проверяем условия для импорта в зависимости от активной вкладки
        if (selectedFile == null || isImporting)
            return false;
            
        if (_activeTabIndex == 0) // Вкладка "Импорт в существующую таблицу"
        {
            return !string.IsNullOrEmpty(selectedTable) && tableStructureModel.Count > 0;
        }
        else // Вкладка "Импорт в новую таблицу"
        {
            return !string.IsNullOrEmpty(newTableName) && columns.Count > 0;
        }
    }

    private async Task StartImport()
    {
        if (!CanStartImport())
        {
            _snackbar.Add("Необходимо выбрать таблицу, настроить структуру и выбрать файл для импорта", Severity.Warning);
            return;
        }
    
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
    
            if (_activeTabIndex == 1) // Вкладка "Импорт в новую таблицу"
            {
                // Сначала создаем новую таблицу
                await CreateTable();
                
                // Если таблица создана успешно, загружаем ее структуру для импорта
                await LoadTableStructure(newTableName);
                selectedTable = newTableName;
            }
                
            // Формирование запроса на импорт
            List<ColumnInfo> columnsForImport;
            string targetTable;
            
            if (_activeTabIndex == 0) // Вкладка "Импорт в существующую таблицу"
            {
                columnsForImport = tableStructureModel;
                targetTable = selectedTable;
            }
            else // Вкладка "Импорт в новую таблицу"
            {
                // Преобразуем структуру новой таблицы в модель для импорта
                columnsForImport = columns.Select(col => new ColumnInfo
                {
                    Name = col.Name,
                    Type = col.Type,
                    IsRequired = col.IsRequired,
                }).ToList();
                targetTable = newTableName;
            }
            
            // Создаем модель запроса импорта
            var importRequest = new TableImportRequestModel
            {
                TableName = targetTable,
                Columns = columnsForImport,
                ImportMode = _dataImportMode,
                Encoding = fileEncoding,
                Delimiter = csvDelimiter,
                HasHeaderRow = hasHeaderRow,
                SkipRows = 0,
                XmlRootElement = xmlRootElement,
                XmlRowElement = xmlRowElement
            };
            
            // Отправляем запрос через клиентский сервис
            var response = await _dataImportClientService.ImportData(selectedFile, importRequest);
            
            if (response.Success)
            {
                importResult = response;
                
                if (importResult.Success)
                {
                    _snackbar.Add("Импорт данных успешно завершен", Severity.Success);
                }
                else
                {
                    _snackbar.Add($"Импорт завершен с ошибками. Проверьте отчет.", Severity.Warning);
                }
            }
            else
            {
                _snackbar.Add($"Ошибка при импорте данных: {response.Message}", Severity.Error);
                
                if (response.Errors != null && response.Errors.Any())
                {
                    // Создаем объект результата импорта с ошибками для отображения
                    importResult = new ImportResult
                    {
                        Success = false,
                        Message = response.Message,
                        RowsProcessed = 0,
                        ErrorCount = response.Errors.Count,
                        Errors = response.Errors.Select(e => new ImportError 
                        { 
                            RowNumber = 0, 
                            ErrorMessage = e.ErrorMessage
                        }).ToList()
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Ошибка: {ex.Message}", Severity.Error);
            Console.Error.WriteLine($"Exception during import: {ex}");
            
            // Создаем объект результата с информацией об ошибке
            importResult = new ImportResult
            {
                Success = false,
                Message = $"Произошла ошибка при импорте: {ex.Message}",
                RowsProcessed = 0,
                ErrorCount = 1,
                Errors = new List<ImportError> 
                { 
                    new ImportError 
                    { 
                        RowNumber = 0, 
                        ErrorMessage = ex.Message
                    }
                }
            };
        }
        finally
        {
            isImporting = false;
            StateHasChanged();
        }
    }

    #region Методы для создания новой таблицы
    
    private void AddColumn()
    {
        if (string.IsNullOrWhiteSpace(newColumnName))
        {
            _snackbar.Add("Введите название колонки.", Severity.Warning);
            return;
        }

        // Проверяем, что колонка с таким именем еще не существует
        if (columns.Any(c => c.Name.Equals(newColumnName, StringComparison.OrdinalIgnoreCase)))
        {
            _snackbar.Add("Колонка с таким именем уже существует.", Severity.Warning);
            return;
        }

        columns.Add(new ColumnInfo
        {
            Name = newColumnName,
            // Type = newColumnType,
            IsRequired = newColumnIsRequired,
            IsPrimaryKey = newColumnIsPrimaryKey
        });
        
        _snackbar.Add($"Колонка '{newColumnName}' добавлена.", Severity.Success);
        
        // Сбрасываем значения для следующего добавления
        newColumnName = string.Empty;
        newColumnType = ColumnTypes.Text;
        newColumnIsPrimaryKey = false;
        newColumnIsRequired = true;
    }
    
    private void RemoveColumn(ColumnInfo column)
    {
        columns.Remove(column);
        _snackbar.Add($"Колонка '{column.Name}' удалена.", Severity.Info);
    }

    private async Task CreateTable()
    {
        if (string.IsNullOrWhiteSpace(newTableName))
        {
            _snackbar.Add("Введите название таблицы.", Severity.Warning);
            return;
        }

        if (columns.Count == 0)
        {
            _snackbar.Add("Добавьте хотя бы одну колонку.", Severity.Warning);
            return;
        }

        // Проверяем, есть ли первичный ключ
        if (!columns.Any(c => c.IsPrimaryKey))
        {
            var parameters = new DialogParameters
            {
                { "ContentText", "Таблица не содержит первичного ключа. Продолжить?" },
                { "ButtonText", "Продолжить без первичного ключа" },
                { "Color", Color.Warning }
            };
            
            var dialog = _dialogService.Show<ConfirmDialog>("Предупреждение", parameters);
            var result = await dialog.Result;
            
            if (result.Canceled)
                return;
        }

        var tableData = new TableModel
        {
            TableName = newTableName,
            Columns = columns,
            PrimaryKey = columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name ?? columns[0].Name,
            TableData = new List<string>()
        };

        try
        {
            var result = await _databaseService.CreateTablesAsync(tableData);
            _snackbar.Add("Таблица успешно создана!", Severity.Success);
            
            // После создания таблицы, обновляем список доступных таблиц
            await LoadAvailableTables();
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Ошибка при создании таблицы: {ex.Message}", Severity.Error);
            throw; // Пробрасываем исключение дальше для обработки в StartImport
        }
    }
    
    private string DetermineDefaultFormatFromColumnType(ColumnTypes columnType)
    {
        // Определяем форматы по умолчанию для разных типов колонок
        return columnType switch
        {
            ColumnTypes.Date => "yyyy-MM-dd",
            _ => ""
        };
    }
    
    #endregion

    private void ResetForm()
    {
        // Сбрасываем общие поля
        selectedFile = null;
        importResult = null;
        
        // Если мы в режиме новой таблицы, сбрасываем поля для новой таблицы
        if (_activeTabIndex == 1)
        {
            newTableName = string.Empty;
            newColumnName = string.Empty;
            newColumnType = ColumnTypes.Text;
            newColumnIsPrimaryKey = false;
            newColumnIsRequired = true;
            columns.Clear();
        }
        
        StateHasChanged();
    }
}