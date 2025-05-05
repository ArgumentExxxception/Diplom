using Core.Models;
using Core.Results;
using Core.ServiceInterfaces;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class FileExportService: IFileExportService
{
 private readonly IXmlExportService _xmlExportService;
    private readonly ICsvExportService _csvExportService;
    private readonly ILogger<FileExportService> _logger;

    public FileExportService(
        IXmlExportService xmlExportService,
        ICsvExportService csvExportService,
        ILogger<FileExportService> logger)
    {
        _xmlExportService = xmlExportService;
        _csvExportService = csvExportService;
        _logger = logger;
    }

    public async Task<(ExportResult Result, Stream FileStream)> ExportDataAsync(
        TableExportRequestModel exportRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Начало процесса экспорта. Таблица: {TableName}, Формат: {Format}, Пользователь: {UserEmail}",
                exportRequest.TableName, exportRequest.ExportFormat, exportRequest.UserEmail);

            var outputStream = new MemoryStream();
            ExportResult result;

            if (string.Equals(exportRequest.ExportFormat, ExportFormat.XML.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                result = await _xmlExportService.ExportToXmlAsync(exportRequest, outputStream, cancellationToken);
            }
            else if (string.Equals(exportRequest.ExportFormat, ExportFormat.CSV.ToString(),
                         StringComparison.OrdinalIgnoreCase))
            {
                result = await _csvExportService.ExportToCsvAsync(exportRequest, outputStream, cancellationToken);
            }
            else
            {
                throw new ArgumentException($"Неподдерживаемый формат экспорта: {exportRequest.ExportFormat}");
            }

            _logger.LogInformation(
                "Завершение процесса экспорта. Таблица: {TableName}, Формат: {Format}, Экспортировано строк: {RowCount}, Время: {ElapsedTime}мс",
                exportRequest.TableName, exportRequest.ExportFormat, result.RowsExported, result.ElapsedTimeMs);

            return (result, outputStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте данных из таблицы {TableName}", exportRequest.TableName);
            throw;
        }
    }
}