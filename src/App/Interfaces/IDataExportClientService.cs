using Core.Models;

namespace App.Interfaces;

public interface IDataExportClientService
{
    /// <summary>
    /// Экспортирует данные из таблицы в указанном формате
    /// </summary>
    /// <param name="exportRequest">Параметры экспорта</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Задача, представляющая асинхронную операцию</returns>
    Task<string> ExportDataAsync(TableExportRequestModel exportRequest, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Получает оценку размера данных для экспорта
    /// </summary>
    /// <param name="tableName">Имя таблицы</param>
    /// <param name="filterCondition">Условие фильтрации</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Оценка размера в байтах</returns>
    Task<long> GetExportDataSizeAsync(string tableName, string filterCondition = null, CancellationToken cancellationToken = default);
}