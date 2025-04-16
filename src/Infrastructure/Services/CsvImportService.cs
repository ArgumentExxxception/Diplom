using System.Diagnostics;
using System.Globalization;
using System.Text;
using Core;
using Core.Errors;
using Core.Models;
using Core.Results;
using Core.ServiceInterfaces;
using Core.Utils;
using CsvHelper;
using CsvHelper.Configuration;
using Domain.Enums;

namespace Infrastructure.Services;

public class CsvImportService: ICsvImportService
{
    private readonly IDataImportRepository _dataImportRepository;

    public CsvImportService(IDataImportRepository dataImportRepository)
    {
        _dataImportRepository = dataImportRepository;
    }

public async Task ProcessCSVFileAsync(
    Stream fileStream,
    TableImportRequestModel importRequest,
    string userName,
    ImportResult importResult,
    CancellationToken cancellationToken)
    {
        try
        {
            List<Dictionary<string, object>> existingData = new List<Dictionary<string, object>>();
            cancellationToken.ThrowIfCancellationRequested();

            if ((ImportMode)importRequest.ImportMode == ImportMode.Replace)
            {
                await _dataImportRepository.ClearTableAsync(importRequest.TableName, cancellationToken);
            }

            if (!importRequest.IsNewTable)
            {
                existingData = await _dataImportRepository.GetExistingDataAsync(importRequest.TableName, cancellationToken);
            }

            using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            // Если в CSV есть заголовки, пропускаем их
            if (importRequest.HasHeaderRow)
            {
                await csv.ReadAsync();
                csv.ReadHeader();
            }
            
            // Пропускаем дополнительные строки согласно параметру SkipRows
            if (importRequest.SkipRows > 0)
            {
                for (int i = 0; i < importRequest.SkipRows; i++)
                {
                    // Читаем и пропускаем строку, если она существует
                    if (!await csv.ReadAsync())
                    {
                        break;
                    }
                }
            }

            var rowsToImport = new List<Dictionary<string, object>>();
            var duplicatedRows = new List<Dictionary<string, object>>();
            int rowIndex = 0;

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowIndex++;

                var rowData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                bool rowHasErrors = false;

                for (int i = 0; i < importRequest.Columns.Where(x => x.Name != DataProcessingUtils.MODIFIED_BY_COLUMN && x.Name != DataProcessingUtils.MODIFIED_BY_COLUMN).Count(); i++)
                {
                    var column = importRequest.Columns[i];
                    string value = csv.GetField(i);

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            var typedValue = DataProcessingUtils.ConvertToTargetType(value, column.Type);
                            rowData[column.Name] = typedValue;
                        }
                        else if (column.IsRequired)
                        {
                            rowHasErrors = true;
                            importResult.ErrorCount++;
                            importResult.Errors.Add(new ImportError
                            {
                                RowNumber = rowIndex,
                                ErrorMessage = $"Отсутствует обязательное поле '{column.Name}'"
                            });
                        }
                        else
                        {
                            rowData[column.Name] = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        rowHasErrors = true;
                        importResult.ErrorCount++;
                        importResult.Errors.Add(new ImportError
                        {
                            RowNumber = rowIndex,
                            ErrorMessage = $"Ошибка в элементе '{column.Name}': {ex.Message}. Значение: '{value}'"
                        });
                    }
                }

                // Проверка всех обязательных полей (на случай, если их больше, чем в CSV)
                foreach (var column in importRequest.Columns.Where(c =>
                             c.IsRequired && c.Name != DataProcessingUtils.MODIFIED_BY_COLUMN && c.Name != DataProcessingUtils.MODIFIED_DATE_COLUMN))
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

                // Устанавливаем служебные поля
                rowData[DataProcessingUtils.MODIFIED_DATE_COLUMN] = DateTime.UtcNow;
                rowData[DataProcessingUtils.MODIFIED_BY_COLUMN] = userName;

                if (!rowHasErrors)
                {
                    if (!importRequest.IsNewTable && existingData.Count > 0)
                    {
                        if (DataProcessingUtils.IsDuplicate(rowData, existingData, importRequest.Columns.ToList()))
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

                // Пакетная обработка
                if (rowsToImport.Count >= 1000)
                {
                    await ImportDataInParallelAsync(importRequest.TableName, rowsToImport, new TableModel { Columns = importRequest.Columns, TableName = importRequest.TableName });
                    rowsToImport.Clear();
                }
            }

            importResult.DuplicatedRows = duplicatedRows;

            // Импортируем оставшиеся строки
            if (rowsToImport.Count > 0)
            {
                await _dataImportRepository.ImportDataBatchAsync(importRequest.TableName, rowsToImport, new TableModel { Columns = importRequest.Columns, TableName = importRequest.TableName });
            }
            importResult.RowsUpdated = importResult.RowsProcessed - importResult.RowsInserted - importResult.RowsSkipped - importResult.ErrorCount;
        }
        catch (Exception ex)
        {
            throw new Exception("Ошибка при обработке CSV: " + ex.Message, ex);
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