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
    ImportResult result,
    CancellationToken cancellationToken)
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
                                var typedValue = DataProcessingUtils.ConvertToTargetType(columnValue, column.Type);
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

                foreach (var column in importRequest.Columns.Where(c => c.IsRequired && c.Name != DataProcessingUtils.MODIFIED_BY_COLUMN && c.Name != DataProcessingUtils.MODIFIED_DATE_COLUMN))
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

                rowData[DataProcessingUtils.MODIFIED_DATE_COLUMN] = DateTime.UtcNow;
                rowData[DataProcessingUtils.MODIFIED_BY_COLUMN] = userName;

                if (!rowHasErrors)
                {
                    if (!importRequest.IsNewTable && existingData.Count > 0)
                    {
                        if (DataProcessingUtils.IsDuplicate(rowData, existingData, importRequest.Columns.ToList()))
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
}