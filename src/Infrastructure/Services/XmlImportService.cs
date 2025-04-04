using System.Xml;
using Core;
using Core.Errors;
using Core.Models;
using Core.Results;
using Core.ServiceInterfaces;
using Core.Utils;
using Domain.Enums;

namespace Infrastructure.Services;

public class XmlImportService: IXmlImportService
{
    private readonly IDataImportRepository _dataImportRepository;

    public XmlImportService(IDataImportRepository dataImportRepository)
    {
        _dataImportRepository = dataImportRepository;
    }

    /// <summary>
    /// Обрабатывает XML файл и импортирует данные
    /// </summary>
    public async Task ProcessXMLFileAsync(
        Stream fileStream,
        TableImportRequestModel importRequest,
        string userName,
        ImportResult importResult,
        CancellationToken cancellationToken)
    {
        try
        {
            List<Dictionary<string,object>> existingData = new List<Dictionary<string, object>>();
            
            cancellationToken.ThrowIfCancellationRequested();
            
            if ((ImportMode)importRequest.ImportMode == ImportMode.Replace)
            {
                await _dataImportRepository.ClearTableAsync(importRequest.TableName, cancellationToken);
            }

            if (!importRequest.IsNewTable)
            {
                existingData = await _dataImportRepository.GetExistingDataAsync(importRequest.TableName, cancellationToken);
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
                                        importResult.ErrorCount++;
                                        importResult.Errors.Add(new ImportError
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
                                        var typedValue = DataProcessingUtils.ConvertToTargetType(columnValue, column.Type);
                                        rowData[column.Name] = typedValue;
                                    }
                                    else if (column.IsRequired)
                                    {
                                        // Если значение пустое, а колонка обязательная
                                        rowHasErrors = true;
                                        importResult.ErrorCount++;
                                        importResult.Errors.Add(new ImportError
                                        {
                                            RowNumber = rowIndex,
                                            ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                                        });
                                        
                                    }
                                }
                                catch (Exception ex)
                                {
                                    rowHasErrors = true;
                                    importResult.ErrorCount++;
                                    importResult.Errors.Add(new ImportError
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
                    foreach (var column in importRequest.Columns.Where(c => c.IsRequired && c.Name != DataProcessingUtils.MODIFIED_BY_COLUMN && c.Name != DataProcessingUtils.MODIFIED_DATE_COLUMN))
                    {
                        if (!rowData.ContainsKey(column.Name) || rowData[column.Name] == null)
                        {
                            rowHasErrors = true;
                            importResult.ErrorCount++;
                            importResult.Errors.Add(new ImportError
                            {
                                RowNumber = rowIndex,
                                ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                            });
                        }
                    }
                    
                    
                    rowData[DataProcessingUtils.MODIFIED_DATE_COLUMN] = DateTime.UtcNow;
                    rowData[DataProcessingUtils.MODIFIED_BY_COLUMN] = userName;


                    // Если нет ошибок, добавляем строку к импорту
                    if (!rowHasErrors)
                    {
                        if (!importRequest.IsNewTable && existingData.Count > 0)
                        {
                            if (DataProcessingUtils.IsDuplicate(rowData, existingData,
                                    importRequest.Columns.ToList()))
                            {
                                duplicatedRows.Add(rowData);
                                importResult.RowsSkipped++;
                            }
                            else
                            {
                                rowsToImport.Add(rowData);
                                importResult.RowsInserted++;
                            }

                        }
                        else
                        {
                            rowsToImport.Add(rowData);
                            importResult.RowsInserted++;
                        }

                    }

                    importResult.RowsProcessed++;

                    // Пакетная обработка для экономии памяти
                    if (rowsToImport.Count >= 1000)
                    {
                        await ImportDataInParallelAsync(importRequest.TableName, rowsToImport, new TableModel{ Columns = importRequest.Columns, TableName = importRequest.TableName });
                        rowsToImport.Clear();
                    }
                }
            }
            
            importResult.DuplicatedRows = duplicatedRows;
            
            // Импортируем оставшиеся строки
            if (rowsToImport.Count > 0)
            {
                await _dataImportRepository.ImportDataBatchAsync(importRequest.TableName, rowsToImport, new TableModel{ Columns = importRequest.Columns, TableName = importRequest.TableName });
            }
            importResult.RowsUpdated = importResult.RowsProcessed - importResult.RowsInserted - importResult.RowsSkipped - importResult.ErrorCount;
        }
        catch (Exception ex)
        {
            // importResult.ErrorCount++;
            // result.Errors.Add(new ImportError
            // {
            //     // RowNumber = rowIndex,
            //     ErrorMessage = $"Ошибка при обработке XML: {ex.Message}"
            // });
            throw new Exception();
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
}