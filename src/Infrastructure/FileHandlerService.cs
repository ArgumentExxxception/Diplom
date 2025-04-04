using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Core;
using Core.Errors;
using Core.Models;
using Core.Results;
using Core.ServiceInterfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Domain.Enums;

namespace Infrastructure;

public class FileHandlerService: IFileHandlerService
{
    private readonly IXmlImportService _xmlImportService;
    private readonly ICsvImportService _csvImportService;
    private readonly IUnitOfWork _unitOfWork;
    // private readonly IAppLogger<FileHandlerService> _logger;
    
    public FileHandlerService(IUnitOfWork unitOfWork, IXmlImportService xmlImportService, ICsvImportService csvImportService)
    {
        _unitOfWork = unitOfWork;
        _xmlImportService = xmlImportService;
        _csvImportService = csvImportService;
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
                await _xmlImportService.ProcessXMLFileAsync(stream, importRequest, userEmail, result, cancellationToken);
            
            else if (IsCSVFile(fileName, contentType))
                await _csvImportService.ProcessCSVFileAsync(stream, importRequest, userEmail, result, cancellationToken);
            
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

    // public async Task UpdateDublicates(string tableName, List<Dictionary<string, object>> dublicates)
    // {
    //     // Получаем существующие данные из таблицы
    //     var existingData = await _dataImportRepository.GetExistingDataAsync(tableName);
    //     var columns = await _databaseService.GetColumnInfoAsync(tableName);
    //     var primaryKeys = columns.Where(x => x.IsPrimaryKey).Select(x => x.Name).ToList();
    //     
    //     var convertedDublicates = dublicates
    //         .Select(row => row.ToDictionary(
    //             kv => kv.Key,
    //             kv => ConvertJsonElementToPrimitive(kv.Value)))
    //         .ToList();
    //
    //     // Фильтруем данные, чтобы оставить только дубликаты
    //     var duplicates = convertedDublicates
    //         .Where(newData => existingData.Any(existingRow => IsDuplicate(newData, new List<Dictionary<string, object>> { existingRow }, primaryKeys)))
    //         .ToList();
    //
    //     if (!duplicates.Any())
    //     {
    //         throw new InvalidOperationException("Дубликаты не найдены.");
    //     }
    //
    //     // Для каждого дубликата находим соответствующую строку и обновляем её
    //     foreach (var duplicate in duplicates)
    //     {
    //         // Находим строку, которую нужно обновить
    //         var duplicateRow = existingData.FirstOrDefault(existingRow => IsDuplicate(duplicate, new List<Dictionary<string, object>> { existingRow }, primaryKeys));
    //
    //         if (duplicateRow == null)
    //         {
    //             throw new InvalidOperationException("Дубликат не найден в существующих данных.");
    //         }
    //
    //         // Передаем данные в сервис для выполнения обновлений в транзакции
    //         await _dataImportRepository.UpdateDuplicatedRows(tableName, new List<Dictionary<string, object>> { duplicate }, primaryKeys, duplicateRow);
    //     }
    // }

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
        
       
    #endregion
    
    

}