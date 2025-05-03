using System.Globalization;
using System.Text;
using Core;
using Core.Errors;
using Core.Models;
using Core.Results;
using Core.ServiceInterfaces;
using Core.Utils;
using CsvHelper;
using Domain.Enums;

namespace Infrastructure.Services;

public class CsvImportService: ICsvImportService
{
    private readonly IDataImportRepository _dataImportRepository;
    private readonly IDatabaseService _databaseService;

    public CsvImportService(IDataImportRepository dataImportRepository, IDatabaseService databaseService)
    {
        _dataImportRepository = dataImportRepository;
        _databaseService = databaseService;
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
            List<string> keyColumns = importRequest.Columns.Any(x => x.SearchInDuplicates)
                ? importRequest.Columns
                    .Where(c => c.SearchInDuplicates)
                    .Select(c => c.Name)
                    .ToList()
                : [];
            
            HashSet<string> existingKeys = new();
            
            if ((ImportMode)importRequest.ImportMode == ImportMode.Replace)
            {
                await _dataImportRepository.ClearTableAsync(importRequest.TableName, cancellationToken);
            }

            if (importRequest.IsNewTable)
            {
                cancellationToken.ThrowIfCancellationRequested();
        
                await _databaseService.CreateTableAsync(new TableModel{ Columns = importRequest.Columns, TableName = importRequest.TableName });
                await _dataImportRepository.CreateIndexesForDuplicateColumnsAsync(importRequest.TableName, importRequest.Columns, cancellationToken);
                await _dataImportRepository.SaveColumnMetadataAsync(importRequest.TableName, importRequest.Columns, cancellationToken);
            }
            else if (!importRequest.IsNewTable && keyColumns.Count > 0)
            {
                existingKeys = await _dataImportRepository
                    .GetExistingRowKeysAsync(importRequest.TableName, keyColumns, cancellationToken);
            }

            using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            
            if (importRequest.HasHeaderRow)
            {
                await csv.ReadAsync();
                csv.ReadHeader();
            }
            
            if (importRequest.SkipRows > 0)
            {
                for (int i = 0; i < importRequest.SkipRows; i++)
                {
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

                for (int i = 0; i < importRequest.Columns.Count(x => x.Name != DataProcessingUtils.MODIFIED_BY_COLUMN && x.Name != DataProcessingUtils.MODIFIED_DATE_COLUMN); i++)
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
                
                if (!rowHasErrors)
                {
                    if (!importRequest.IsNewTable && importRequest.ImportMode == (int)ImportMode.Insert && keyColumns.Count > 0)
                    {
                        string rowKey = string.Join("::", keyColumns.Select(col =>
                            rowData.TryGetValue(col, out var val) ? val?.ToString()?.Trim() ?? "" : ""));

                        if (existingKeys.Contains(rowKey))
                        {
                            duplicatedRows.Add(rowData);
                            importResult.RowsSkipped++;
                        }
                        else
                        {
                            rowsToImport.Add(rowData);
                            importResult.RowsInserted++;
                            existingKeys.Add(rowKey); // чтобы не вставить дубликат в рамках импорта
                        }

                    }
                    else
                    {
                        rowsToImport.Add(rowData);
                        importResult.RowsInserted++;
                    }
                }

                importResult.RowsProcessed++;

                if (rowsToImport.Count >= 1000)
                {
                    await ImportDataInParallelAsync(importRequest.TableName, rowsToImport, new TableModel { Columns = importRequest.Columns, TableName = importRequest.TableName }, userName, cancellationToken);
                    rowsToImport.Clear();
                }
            }

            importResult.DuplicatedRows = duplicatedRows;

            if (rowsToImport.Count > 0)
            {
                await _dataImportRepository.ImportDataBatchAsync(importRequest.TableName, rowsToImport, new TableModel { Columns = importRequest.Columns, TableName = importRequest.TableName }, userName, cancellationToken);
            }
            importResult.RowsUpdated = importResult.RowsProcessed - importResult.RowsInserted - importResult.RowsSkipped - importResult.ErrorCount;
        }
        catch (Exception ex)
        {
            throw new Exception("Ошибка при обработке CSV: " + ex.Message, ex);
        }
    }

    private async Task ImportDataInParallelAsync(
        string tableName,
        List<Dictionary<string, object>> rowsToImport,
        TableModel tableModel,
        string userName,
        CancellationToken cancellationToken,
        int batchSize = 1000,
        int maxParallelism = 4)
    {
        if (rowsToImport.Count == 0) return;

        var batches = rowsToImport
            .Select((row, index) => new { row, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.row).ToList());

        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = maxParallelism },
            async (batch, _) =>
            {
                await _dataImportRepository.ImportDataBatchAsync(tableName, batch, tableModel, userName, cancellationToken);
            });
    }
}