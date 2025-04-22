using System.Diagnostics;
using System.Text.Json;
using Core;
using Core.Errors;
using Core.Models;
using Core.Results;
using Core.ServiceInterfaces;

namespace Infrastructure;

public class FileHandlerService: IFileHandlerService
{
    private readonly IXmlImportService _xmlImportService;
    private readonly ICsvImportService _csvImportService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataImportRepository _dataImportRepository;
    // private readonly IAppLogger<FileHandlerService> _logger;
    
    public FileHandlerService(IDataImportRepository dataImportRepository,IUnitOfWork unitOfWork, IXmlImportService xmlImportService, ICsvImportService csvImportService)
    {
        _dataImportRepository = dataImportRepository;
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
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Произошла ошибка при импорте: {ex.Message}";
            result.ErrorCount++;
            result.Errors.Add(new ImportError
            {
                RowNumber = 0,
                ErrorMessage = $"Общая ошибка: {ex.Message}"
            });
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    public async Task UpdateDuplicatesAsync(string tableName, List<Dictionary<string, object>> duplicatedRows, List<ColumnInfo> columns, string userEmail,
        CancellationToken cancellationToken = default)
    {
        if (duplicatedRows == null || duplicatedRows.Count == 0)
            return;

        var searchColumns = columns
            .Where(c => c.SearchInDuplicates)
            .Select(c => c.Name)
            .ToList();

        var filters = duplicatedRows.Select(row =>
            searchColumns.ToDictionary(col => col, col => row[col])
        ).ToList();

        await _dataImportRepository.DeleteDuplicatesAsync(tableName, filters, cancellationToken);

        await _dataImportRepository.ImportDataBatchAsync(tableName, duplicatedRows, new TableModel
        {
            TableName = tableName,
            Columns = columns
        }, userEmail, cancellationToken);
    }

    #region Вспомогательные методы
        private bool IsCSVFile(string fileName, string contentType)
        {
            return contentType.Contains("csv") ||
                   contentType.Contains("text/plain") ||
                   fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        }
        
        private bool IsXMLFile(string fileName, string contentType)
        {
            return contentType.Contains("xml") ||
                   fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
        }
        
       
    #endregion
    
    

}