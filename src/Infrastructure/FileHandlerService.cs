using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Core;
using Core.Errors;
using Core.Models;
using Core.Results;
using CsvHelper;
using CsvHelper.Configuration;
using Domain.Enums;

namespace Infrastructure;

public class FileHandlerService: IFileHandlerService
{
    private readonly IDataImportRepository _dataImportRepository;
    private readonly IDatabaseService _databaseService;
    private readonly IUnitOfWork _unitOfWork;
    // private readonly IAppLogger<FileHandlerService> _logger;
    
    private const string MODIFIED_DATE_COLUMN = "lastmodifiedon";
    private const string MODIFIED_BY_COLUMN = "lastmodifiedby";
    
    public FileHandlerService(IDataImportRepository dataImportRepository, IDatabaseService databaseService, IUnitOfWork unitOfWork)
    {
        _dataImportRepository = dataImportRepository;
        _databaseService = databaseService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResult> ImportDataAsync(Stream stream, string fileName, string contentType,
        TableImportRequestModel importRequest, CancellationToken cancellationToken)
    {
        if (stream == null) 
            throw new ArgumentNullException(nameof(stream));
        
        if (string.IsNullOrEmpty(fileName)) 
            throw new ArgumentException("Имя файла не может быть пустым", nameof(fileName));
        
        if (importRequest == null) 
            throw new ArgumentNullException(nameof(importRequest));
        
        if (string.IsNullOrEmpty(importRequest.UserEmail))
            throw new UnauthorizedAccessException("Зарегистрируйтесь или авторизуйтесь! Доступ запрещен.");

        var user = await _unitOfWork.Users.GetByEmailAsync(importRequest.UserEmail);
        string userEmail = string.Empty;
        
        if (user == null)
            throw new UnauthorizedAccessException($"Пользователь с почтой {importRequest.UserEmail} не найден в базе!");
        else
            userEmail = user.Username;

        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult
        {
            Success = false,
            RowsProcessed = 0,
            ErrorCount = 0,
            Errors = new List<ImportError>()
        };

        try
        {
            // _logger.LogInformation(
            //     "Начало процесса импорта. Файл: {FileName}, Таблица: {TableName}, Пользователь: {UserName}",
            //     fileName, importRequest.TableName, userName);
            cancellationToken.ThrowIfCancellationRequested();
            
            if (IsXMLFile(fileName, contentType))
                await ProcessXMLFileAsync(stream, importRequest, userEmail, result, cancellationToken);
            
            else if (IsCSVFile(fileName, contentType))
                await ProcessCSVFileAsync(stream, importRequest, userEmail, result);
            
            else
                throw new FormatException(
                    $"Неподдерживаемый формат файла: {contentType}. Поддерживаются только CSV и XML.");
            
            
            result.Success = result.ErrorCount == 0;
            result.Message = result.Success
                ? $"Импорт успешно завершен. Обработано {result.RowsProcessed} строк."
                : $"Импорт завершен с ошибками. Обработано {result.RowsProcessed} строк, найдено {result.ErrorCount} ошибок.";

            // Логируем результат операции
            // await _operationLogService.LogImportOperationAsync(
            //     importRequest.TableName,
            //     fileName,
            //     userName,
            //     result.ProcessedRowsCount,
            //     result.ErrorsCount,
            //     stopwatch.ElapsedMilliseconds,
            //     result.Success);
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Ошибка при импорте данных из файла {FileName}", fileName);
            result.Success = false;
            result.Message = $"Произошла ошибка при импорте: {ex.Message}";
            result.ErrorCount++;
            result.Errors.Add(new ImportError
            {
                RowNumber = 0,
                ErrorMessage = $"Общая ошибка: {ex.Message}"
            });

            // Логируем ошибку операции
            // await _operationLogService.LogImportOperationAsync(
            //     importRequest.TableName,
            //     fileName,
            //     userName,
            //     result.ProcessedRowsCount,
            //     result.ErrorsCount,
            //     stopwatch.ElapsedMilliseconds,
            //     false,
            //     ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedTimeMs = stopwatch.ElapsedMilliseconds;

            // _logger.LogInformation(
            //     "Завершение процесса импорта. Файл: {FileName}, Таблица: {TableName}, Строк: {RowCount}, Ошибок: {ErrorCount}, Время: {ElapsedTime}мс",
            //     fileName, importRequest.TableName, result.ProcessedRowsCount, result.ErrorsCount, result.ElapsedTimeMs);
        }

        return result;
    }
    
    private object ConvertJsonElementToPrimitive(object value)
    {
        if (value is JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    return jsonElement.GetString();
                case JsonValueKind.Number:
                    if (jsonElement.TryGetInt32(out int intValue))
                        return intValue;
                    if (jsonElement.TryGetDouble(out double doubleValue))
                        return doubleValue;
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return jsonElement.GetBoolean();
                case JsonValueKind.Null:
                    return null;
                default:
                    throw new NotSupportedException($"Unsupported JsonValueKind: {jsonElement.ValueKind}");
            }
        }
        return value;
    }

    public async Task UpdateDublicates(string tableName, List<Dictionary<string, object>> dublicates)
    {
        // Получаем существующие данные из таблицы
        var existingData = await _dataImportRepository.GetExistingDataAsync(tableName);
        var columns = await _databaseService.GetColumnInfoAsync(tableName);
        var primaryKeys = columns.Where(x => x.IsPrimaryKey).Select(x => x.Name).ToList();
        
        var convertedDublicates = dublicates
            .Select(row => row.ToDictionary(
                kv => kv.Key,
                kv => ConvertJsonElementToPrimitive(kv.Value)))
            .ToList();

        // Фильтруем данные, чтобы оставить только дубликаты
        var duplicates = convertedDublicates
            .Where(newData => existingData.Any(existingRow => IsDuplicate(newData, new List<Dictionary<string, object>> { existingRow }, primaryKeys)))
            .ToList();

        if (!duplicates.Any())
        {
            throw new InvalidOperationException("Дубликаты не найдены.");
        }

        // Для каждого дубликата находим соответствующую строку и обновляем её
        foreach (var duplicate in duplicates)
        {
            // Находим строку, которую нужно обновить
            var duplicateRow = existingData.FirstOrDefault(existingRow => IsDuplicate(duplicate, new List<Dictionary<string, object>> { existingRow }, primaryKeys));

            if (duplicateRow == null)
            {
                throw new InvalidOperationException("Дубликат не найден в существующих данных.");
            }

            // Передаем данные в сервис для выполнения обновлений в транзакции
            await _dataImportRepository.UpdateDuplicatedRows(tableName, new List<Dictionary<string, object>> { duplicate }, primaryKeys, duplicateRow);
        }
    }

    #region Обработчик XML файлов

    /// <summary>
    /// Обрабатывает XML файл и импортирует данные
    /// </summary>
    private async Task ProcessXMLFileAsync(
        Stream fileStream,
        TableImportRequestModel importRequest,
        string userName,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            List<Dictionary<string,object>> existingData = new List<Dictionary<string, object>>();
            
            cancellationToken.ThrowIfCancellationRequested();
            
            if ((ImportMode)importRequest.ImportMode == ImportMode.Replace)
            {
                await _dataImportRepository.ClearTableAsync(importRequest.TableName);
            }

            if (!importRequest.IsNewTable)
            {
                existingData = await _dataImportRepository.GetExistingDataAsync(importRequest.TableName);
            }
            
            // Настройки для XmlReader
            var settings = new XmlReaderSettings
            {
                Async = true,
                IgnoreWhitespace = true,
                IgnoreComments = true,
                CloseInput = false,
            };

            // Определяем корневой элемент и элемент строки
            string rootElement = string.IsNullOrEmpty(importRequest.XmlRootElement) ? "root" : importRequest.XmlRootElement;
            string rowElement = string.IsNullOrEmpty(importRequest.XmlRowElement) ? "row" : importRequest.XmlRowElement;
            
            // Сбрасываем позицию потока
            fileStream.Position = 0;
            using var reader = XmlReader.Create(fileStream, settings);

            // Находим корневой элемент, если он не был указан
            if (string.IsNullOrEmpty(importRequest.XmlRootElement))
            {
                while (await reader.ReadAsync())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        rootElement = reader.Name;
                        break;
                    }
                }
            }

            var rowsToImport = new List<Dictionary<string, object>>();
            var duplicatedRows = new List<Dictionary<string, object>>();
            int rowIndex = 0;

            // Читаем XML документ
            while (await reader.ReadAsync())
            {
                // Ждем, пока не найдем корневой элемент
                if (reader.NodeType == XmlNodeType.Element && (reader.Name == rowElement || reader.Depth == 1))
                {
                    rowIndex++;

                    // Получаем имя текущего элемента как элемента строки, если rowElement не указан
                    if (string.IsNullOrEmpty(importRequest.XmlRowElement))
                    {
                        rowElement = reader.Name;
                    }

                    // Читаем данные строки
                    var rowData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    var rowHasErrors = false;

                    // Если элемент строки пустой (например, <Employee/>), то пропускаем его
                    if (reader.IsEmptyElement)
                    {
                        continue;
                    }

                    // Запоминаем глубину текущего элемента строки
                    int rowDepth = reader.Depth;

                    // Индекс для сопоставления колонок по порядку
                    int columnIndex = 0;

                    // Читаем содержимое элемента строки до его закрывающего тега
                    while (await reader.ReadAsync())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // Если вышли за пределы элемента строки, то закончили его обработку
                        if (reader.Depth <= rowDepth && reader.NodeType == XmlNodeType.EndElement)
                        {
                            break;
                        }

                        // Обрабатываем дочерние элементы (колонки)
                        if (reader.NodeType == XmlNodeType.Element && reader.Depth == rowDepth + 1)
                        {
                            string columnValue = string.Empty;

                            // Проверяем, что индекс колонки не превышает количество колонок в запросе
                            if (columnIndex < importRequest.Columns.Count)
                            {
                                var column = importRequest.Columns[columnIndex];

                                // Если элемент колонки пустой, то значение пустое
                                if (reader.IsEmptyElement)
                                {
                                    // Обрабатываем пустую колонку
                                    if (column.IsRequired)
                                    {
                                        rowHasErrors = true;
                                        result.ErrorCount++;
                                        result.Errors.Add(new ImportError
                                        {
                                            RowNumber = rowIndex,
                                            ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                                        });
                                    }

                                    columnIndex++;
                                    continue;
                                }

                                // Читаем содержимое элемента колонки
                                if (await reader.ReadAsync() && reader.NodeType == XmlNodeType.Text)
                                {
                                    columnValue = reader.Value;
                                }

                                // Пропускаем до конца элемента колонки
                                while (await reader.ReadAsync() &&
                                       !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == rowDepth + 1))
                                {
                                    // Пропускаем содержимое
                                }

                                // Конвертируем значение в нужный тип
                                try
                                {
                                    if (!string.IsNullOrWhiteSpace(columnValue))
                                    {
                                        var typedValue = ConvertToTargetType(columnValue, column.Type);
                                        rowData[column.Name] = typedValue;
                                    }
                                    else if (column.IsRequired)
                                    {
                                        // Если значение пустое, а колонка обязательная
                                        rowHasErrors = true;
                                        result.ErrorCount++;
                                        result.Errors.Add(new ImportError
                                        {
                                            RowNumber = rowIndex,
                                            ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                                        });
                                        
                                    }
                                }
                                catch (Exception ex)
                                {
                                    rowHasErrors = true;
                                    result.ErrorCount++;
                                    result.Errors.Add(new ImportError
                                    {
                                        RowNumber = rowIndex,
                                        ErrorMessage = $"Ошибка в элементе '{column.Name}': {ex.Message}. Значение: '{columnValue}'"
                                    });
                                }

                                columnIndex++;
                            }
                            else
                            {
                                // Если элементов больше, чем колонок в таблице, пропускаем их
                                // Можно добавить логирование
                            }
                        }
                    }

                    // Проверяем, все ли обязательные поля заполнены
                    foreach (var column in importRequest.Columns.Where(c => c.IsRequired && c.Name != MODIFIED_BY_COLUMN && c.Name != MODIFIED_DATE_COLUMN))
                    {
                        if (!rowData.ContainsKey(column.Name) || rowData[column.Name] == null)
                        {
                            rowHasErrors = true;
                            result.ErrorCount++;
                            result.Errors.Add(new ImportError
                            {
                                RowNumber = rowIndex,
                                ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                            });
                        }
                    }
                    
                    
                    rowData[MODIFIED_DATE_COLUMN] = DateTime.UtcNow;
                    rowData[MODIFIED_BY_COLUMN] = userName;


                    // Если нет ошибок, добавляем строку к импорту
                    if (!rowHasErrors)
                    {
                        if (!importRequest.IsNewTable && existingData.Count > 0)
                        {
                            if (IsDuplicate(rowData, existingData,
                                    importRequest.Columns.Where(x => x.IsPrimaryKey).Select(x => x.Name).ToList()))
                            {
                                duplicatedRows.Add(rowData);
                                result.RowsSkipped++;
                            }
                            else
                            {
                                rowsToImport.Add(rowData);
                                result.RowsInserted++;
                            }

                        }
                        else
                        {
                            rowsToImport.Add(rowData);
                            result.RowsInserted++;
                        }

                    }

                    result.RowsProcessed++;

                    // Пакетная обработка для экономии памяти
                    if (rowsToImport.Count >= 1000)
                    {
                        await ImportDataInParallelAsync(importRequest.TableName, rowsToImport, new TableModel{ Columns = importRequest.Columns, TableName = importRequest.TableName });
                        rowsToImport.Clear();
                    }
                }
            }
            
            result.DuplicatedRows = duplicatedRows;
            
            // Импортируем оставшиеся строки
            if (rowsToImport.Count > 0)
            {
                await _dataImportRepository.ImportDataBatchAsync(importRequest.TableName, rowsToImport, new TableModel{ Columns = importRequest.Columns, TableName = importRequest.TableName });
            }
            result.RowsUpdated = result.RowsProcessed - result.RowsInserted - result.RowsSkipped - result.ErrorCount;
        }
        catch (Exception ex)
        {
            result.ErrorCount++;
            result.Errors.Add(new ImportError
            {
                // RowNumber = rowIndex,
                ErrorMessage = $"Ошибка при обработке XML: {ex.Message}"
            });
        }
    }
    
    private async Task ImportDataInParallelAsync(string tableName, List<Dictionary<string, object>> rowsToImport, TableModel tableModel, int maxParallelism = 4)
    {
        if (rowsToImport.Count == 0) return;

        int batchSize = 1000;
        var batches = rowsToImport.Select((row, index) => new { row, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.row).ToList())
            .ToList();

        using var semaphore = new SemaphoreSlim(maxParallelism);
        var tasks = new List<Task>();

        foreach (var batch in batches)
        {
            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _dataImportRepository.ImportDataBatchAsync(tableName, batch, tableModel);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
    }


    #endregion

    #region Обработчик CSV файлов

    private async Task ProcessCSVFileAsync(
    Stream fileStream,
    TableImportRequestModel importRequest,
    string userName,
    ImportResult result)
{
    try
    {
        List<Dictionary<string, object>> existingData = new List<Dictionary<string, object>>();

        string delimeter = importRequest.Delimiter; 
                           // ?? GuessDelimiter(fileStream);

        if ((ImportMode)importRequest.ImportMode == ImportMode.Replace)
        {
            await _dataImportRepository.ClearTableAsync(importRequest.TableName);
        }

        if (!importRequest.IsNewTable)
        {
            existingData = await _dataImportRepository.GetExistingDataAsync(importRequest.TableName);
        }

        // Настройки для чтения CSV
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",", // Разделитель (можно изменить, если используется другой)
            HasHeaderRecord = importRequest.HasHeaderRow, // Указываем, что первая строка — это заголовки
            MissingFieldFound = null, // Игнорируем отсутствующие поля
            BadDataFound = context => // Обработка некорректных данных
            {
                result.ErrorCount++;
                result.Errors.Add(new ImportError
                {
                    // RowNumber = context.,
                    ErrorMessage = $"Некорректные данные в строке {context.Context}: {context.RawRecord}"
                });
            }
        };
        
        fileStream.Position = 0;

        using var reader = new StreamReader(fileStream, Encoding.UTF8);
        Debug.Print($"Encoding: {reader.CurrentEncoding}");
        Debug.Print($"Delimiter: {config.Delimiter}");
        Debug.Print($"HasHeaderRecord: {config.HasHeaderRecord}");
        using var csv = new CsvReader(reader, config);
        
        if (importRequest.HasHeaderRow && await csv.ReadAsync()) 
        {
            csv.ReadHeader();
        }

        var rowsToImport = new List<Dictionary<string, object>>();
        var duplicatedRows = new List<Dictionary<string, object>>();
        int rowIndex = 0;

        while (await csv.ReadAsync())
        {
            rowIndex++;

            var rowData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var rowHasErrors = false;
            int columnIndex = 0;

            foreach (var column in importRequest.Columns)
            {
                try
                {
                    if (columnIndex < csv.Parser.Count)
                    {
                        string columnValue = csv[columnIndex];

                        if (!string.IsNullOrWhiteSpace(columnValue))
                        {
                            var typedValue = ConvertToTargetType(columnValue, column.Type);
                            rowData[column.Name] = typedValue;
                        }
                        else if (column.IsRequired)
                        {
                            rowHasErrors = true;
                            result.ErrorCount++;
                            result.Errors.Add(new ImportError
                            {
                                RowNumber = rowIndex,
                                ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                            });
                        }
                    }
                    else if (column.IsRequired)
                    {
                        rowHasErrors = true;
                        result.ErrorCount++;
                        result.Errors.Add(new ImportError
                        {
                            RowNumber = rowIndex,
                            ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                        });
                    }
                }
                catch (Exception ex)
                {
                    rowHasErrors = true;
                    result.ErrorCount++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowIndex,
                        ErrorMessage = $"Ошибка в элементе '{column.Name}': {ex.Message}"
                    });
                }

                columnIndex++;
            }

            foreach (var column in importRequest.Columns.Where(c => c.IsRequired && c.Name != MODIFIED_BY_COLUMN && c.Name != MODIFIED_DATE_COLUMN))
            {
                if (!rowData.ContainsKey(column.Name) || rowData[column.Name] == null)
                {
                    rowHasErrors = true;
                    result.ErrorCount++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowIndex,
                        ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                    });
                }
            }

            rowData[MODIFIED_DATE_COLUMN] = DateTime.UtcNow;
            rowData[MODIFIED_BY_COLUMN] = userName;

            if (!rowHasErrors)
            {
                if (!importRequest.IsNewTable && existingData.Count > 0)
                {
                    if (IsDuplicate(rowData, existingData, importRequest.Columns.Where(x => x.IsPrimaryKey).Select(x => x.Name).ToList()))
                    {
                        duplicatedRows.Add(rowData);
                        result.RowsSkipped++;
                    }
                    else
                    {
                        rowsToImport.Add(rowData);
                        result.RowsInserted++;
                    }
                }
                else
                {
                    rowsToImport.Add(rowData);
                    result.RowsInserted++;
                }
            }

            result.RowsProcessed++;

            if (rowsToImport.Count >= 1000)
            {
                await _dataImportRepository.ImportDataBatchAsync(importRequest.TableName, rowsToImport, new TableModel { Columns = importRequest.Columns, TableName = importRequest.TableName });
                rowsToImport.Clear();
            }
        }

        if (rowsToImport.Count > 0)
        {
            await _dataImportRepository.ImportDataBatchAsync(importRequest.TableName, rowsToImport, new TableModel { Columns = importRequest.Columns, TableName = importRequest.TableName });
        }

        result.RowsUpdated = result.RowsProcessed - result.RowsInserted - result.RowsSkipped - result.ErrorCount;
        result.DuplicatedRows = duplicatedRows;
    }
    catch (Exception ex)
    {
        result.ErrorCount++;
        result.Errors.Add(new ImportError
        {
            // RowNumber = ,
            ErrorMessage = $"Ошибка при обработке CSV: {ex.Message}"
        });
    }
}
    

    #endregion
    // private string GuessDelimiter(Stream fileStream)
    // {
    //     var delimiters = new[] { ',', ';', '\t', '|' }; // Возможные разделители
    //     fileStream.Position = 0; // Сбрасываем позицию потока
    //
    //     using var reader = new StreamReader(fileStream);
    //     string firstLine = reader.ReadLine();
    //
    //     if (string.IsNullOrEmpty(firstLine))
    //         return ","; // По умолчанию используем запятую
    //
    //     // Определяем наиболее вероятный разделитель
    //     var delimiterCounts = delimiters.ToDictionary(d => d, d => firstLine.Split(d).Length);
    //     var mostLikelyDelimiter = delimiterCounts.OrderByDescending(d => d.Value).First().Key;
    //
    //     fileStream.Position = 0;
    //
    //     return mostLikelyDelimiter.ToString();
    // }
    
    private bool IsDuplicate(
        Dictionary<string, object> rowData,
        List<Dictionary<string, object>> existingData,
        List<string> primaryKeys)
    {
        var excludedColumns = new HashSet<string> { MODIFIED_DATE_COLUMN, MODIFIED_BY_COLUMN };
        
        var filteredRowData = rowData
            .Where(kv => !excludedColumns.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        // Если указаны PrimaryKeyColumns, сравниваем только по ним
        if (primaryKeys.Any())
        {
            return existingData.Any(existingRow =>
            {
                // Удаляем служебные столбцы из existingRow
                var filteredExistingRow = existingRow
                    .Where(kv => !excludedColumns.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                // Сравниваем только по PrimaryKeyColumns
                return primaryKeys
                    .All(pk =>
                        filteredExistingRow.ContainsKey(pk) &&
                        filteredRowData.ContainsKey(pk) &&
                        Equals(filteredExistingRow[pk], filteredRowData[pk]));
            });
        }

        // Если PrimaryKeyColumns не указаны, сравниваем по всем столбцам
        return existingData.Any(existingRow =>
        {
            // Удаляем служебные столбцы из existingRow
            var filteredExistingRow = existingRow
                .Where(kv => !excludedColumns.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            // Сравниваем по всем столбцам
            return filteredExistingRow.Keys
                .All(key =>
                    filteredRowData.ContainsKey(key) &&
                    Equals(filteredExistingRow[key], filteredRowData[key]));
        });
    }

    #region Вспомогательные методы
        /// <summary>
        /// Определяет, является ли файл CSV-файлом
        /// </summary>
        private bool IsCSVFile(string fileName, string contentType)
        {
            return contentType.Contains("csv") ||
                   contentType.Contains("text/plain") ||
                   fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Определяет, является ли файл XML-файлом
        /// </summary>
        private bool IsXMLFile(string fileName, string contentType)
        {
            return contentType.Contains("xml") ||
                   fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Конвертирует строковое значение в целевой тип данных согласно перечислению ColumnTypes
        /// </summary>
        private object ConvertToTargetType(string value, int dataTypeInt)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DBNull.Value;

            value = value.Trim();

            // Пытаемся преобразовать строковое представление типа в ColumnTypes
            ColumnTypes dataType = (ColumnTypes)dataTypeInt;

            try
            {
                switch (dataType)
                {
                    case ColumnTypes.Integer:
                        // Для целых чисел
                        return int.Parse(value, CultureInfo.InvariantCulture);

                    case ColumnTypes.Double:
                        // Для дробных чисел, заменяем запятые на точки для международного формата
                        value = value.Replace(',', '.');
                        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

                    case ColumnTypes.Boolean:
                        // Для логических значений
                        if (bool.TryParse(value, out bool boolResult))
                            return boolResult;
                        
                        // Проверяем различные варианты записи
                        value = value.ToLowerInvariant();
                        if (value == "1" || value == "yes" || value == "y" || value == "да" || value == "true" || value == "t")
                            return true;
                        if (value == "0" || value == "no" || value == "n" || value == "нет" || value == "false" || value == "f")
                            return false;
                        
                        throw new FormatException($"Невозможно преобразовать '{value}' в логический тип");

                    case ColumnTypes.Date:
                        // Для дат
                        string[] dateFormats = new[]
                        {
                            "dd.MM.yyyy", // Например, 15.06.2021
                            "yyyy-MM-dd",  // Например, 2021-06-15
                            "MM/dd/yyyy",  // Например, 06/15/2021
                            "dd/MM/yyyy",  // Например, 15/06/2021
                            "yyyy/MM/dd"   // Например, 2021/06/15
                        };
                
                        if (DateTime.TryParseExact(value, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                        {
                            if (dateValue.Kind == DateTimeKind.Unspecified)
                            {
                                dateValue = DateTime.SpecifyKind(dateValue, DateTimeKind.Utc);
                            }
                            return dateValue;
                        }
                        throw new FormatException("Некорректный формат даты.");
                    
                    case ColumnTypes.Text:
                    default:
                        // Для текстовых типов просто возвращаем строку
                        return value;
                }
            }
            catch (Exception ex)
            {
                throw new FormatException($"Невозможно преобразовать '{value}' в тип {dataType}: {ex.Message}", ex);
            }
        }
        
       
    #endregion
    
    

}