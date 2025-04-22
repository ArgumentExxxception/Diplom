using Core.Models;

namespace Core.ServiceInterfaces;

public interface IFileExportService
{
    /// <summary>
    /// Экспортирует данные из БД в запрошенном формате
    /// </summary>
    /// <param name="exportRequest">Параметры экспорта</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат экспорта и поток с данными</returns>
    Task<(ExportResult Result, Stream FileStream)> ExportDataAsync(
        TableExportRequestModel exportRequest,
        CancellationToken cancellationToken = default);
}