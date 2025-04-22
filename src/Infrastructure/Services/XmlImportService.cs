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
            using var reader = await CreateXmlReaderAsync(fileStream, importRequest, cancellationToken);

            string rootElement = string.IsNullOrEmpty(importRequest.XmlRootElement) ? "root" : importRequest.XmlRootElement;
            string rowElement = string.IsNullOrEmpty(importRequest.XmlRowElement) ? "row" : importRequest.XmlRowElement;

            var rowsToImport = new List<Dictionary<string, object>>();
            var duplicatedRows = new List<Dictionary<string, object>>();
            int rowIndex = 0;

            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element && (reader.Name == rowElement || reader.Depth == 1))
                {
                    rowIndex++;

                    if (string.IsNullOrEmpty(importRequest.XmlRowElement))
                    {
                        rowElement = reader.Name;
                    }
                    
                    var (rowData, rowHasErrors) = await ProcessRowAsync(reader,importRequest,rowIndex,importResult,userName,cancellationToken);

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
                    
                    if (rowsToImport.Count >= 1000)
                    {
                        await ImportDataInParallelAsync(importRequest.TableName, rowsToImport, new TableModel{ Columns = importRequest.Columns, TableName = importRequest.TableName }, userName);
                        rowsToImport.Clear();
                    }
                }
            }
            
            importResult.DuplicatedRows = duplicatedRows;
            
            if (rowsToImport.Count > 0)
            {
                await _dataImportRepository.ImportDataBatchAsync(importRequest.TableName, rowsToImport, new TableModel{ Columns = importRequest.Columns, TableName = importRequest.TableName }, userName, cancellationToken);
            }
            importResult.RowsUpdated = importResult.RowsProcessed - importResult.RowsInserted - importResult.RowsSkipped - importResult.ErrorCount;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }
    
    private async Task<XmlReader> CreateXmlReaderAsync(Stream fileStream, TableImportRequestModel importRequest, CancellationToken cancellationToken)
    {
        fileStream.Position = 0;
        var settings = new XmlReaderSettings
        {
            Async = true,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            CloseInput = false,
        };

        var reader = XmlReader.Create(fileStream, settings);
        
        if (string.IsNullOrEmpty(importRequest.XmlRootElement))
        {
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    importRequest.XmlRootElement = reader.Name;
                    break;
                }
            }
        }

        return reader;
    }
    
    private async Task<(Dictionary<string, object> rowData, bool hasErrors)> ProcessRowAsync(
        XmlReader reader,
        TableImportRequestModel importRequest,
        int rowIndex,
        ImportResult importResult,
        string userName,
        CancellationToken cancellationToken)
    {
        var rowData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        bool rowHasErrors = false;

        if (reader.IsEmptyElement)
        {
            return (rowData, rowHasErrors);
        }

        int rowDepth = reader.Depth;
        int columnIndex = 0;
        
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (reader.Depth <= rowDepth && reader.NodeType == XmlNodeType.EndElement)
            {
                break;
            }
            
            if (reader.NodeType == XmlNodeType.Element && reader.Depth == rowDepth + 1)
            {
                string columnValue = string.Empty;

                if (columnIndex < importRequest.Columns.Count)
                {
                    var column = importRequest.Columns[columnIndex];

                    if (reader.IsEmptyElement)
                    {
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

                    if (await reader.ReadAsync() && reader.NodeType == XmlNodeType.Text)
                    {
                        columnValue = reader.Value;
                    }
                    
                    while (await reader.ReadAsync() &&
                           !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == rowDepth + 1))
                    {
                    }
                    
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(columnValue))
                        {
                            var typedValue = DataProcessingUtils.ConvertToTargetType(columnValue, column.Type);
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

        return (rowData, rowHasErrors);
    }

    
    private async Task ImportDataInParallelAsync(string tableName, List<Dictionary<string, object>> rowsToImport, TableModel tableModel, string userName,int maxParallelism = 4)
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
                    await _dataImportRepository.ImportDataBatchAsync(tableName, batch, tableModel, userName);
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