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
            // Получаем существующие данные для проверки дубликатов (если импорт не для новой таблицы)
            List<Dictionary<string, object>> existingData = new List<Dictionary<string, object>>();
            cancellationToken.ThrowIfCancellationRequested();

            if ((ImportMode)importRequest.ImportMode == ImportMode.Replace)
            {
                await _dataImportRepository.ClearTableAsync(importRequest.TableName, cancellationToken);
            }

            if (!importRequest.IsNewTable)
            {
                existingData =
                    await _dataImportRepository.GetExistingDataAsync(importRequest.TableName, cancellationToken);
            }

            // Сбрасываем позицию потока
            fileStream.Position = 0;

            // Настройка CsvHelper: предполагается, что CSV содержит заголовок,
            // но сопоставление по имени не требуется – данные будут считываться по порядку.
            using var reader = new StreamReader(fileStream);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true, // Заголовок может присутствовать, но мы его не используем для сопоставления
                IgnoreBlankLines = true,
                BadDataFound = null, // Опционально: можно логировать некорректные строки
            };

            using var csv = new CsvReader(reader, config);
            // Пропускаем заголовок, если он есть
            if (config.HasHeaderRecord)
            {
                await csv.ReadAsync();
                csv.ReadHeader();
            }

            var rowsToImport = new List<Dictionary<string, object>>();
            var duplicatedRows = new List<Dictionary<string, object>>();
            int rowIndex =
                config.HasHeaderRecord ? 1 : 0; // если заголовок есть, первая строка с данными будет под номером 2

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowIndex++;

                var rowData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                bool rowHasErrors = false;

                // Обрабатываем данные по порядку:
                // Каждая колонка CSV назначается столбцу из importRequest.Columns согласно порядку.
                for (int colIndex = 0; colIndex < importRequest.Columns.Count; colIndex++)
                {
                    var column = importRequest.Columns[colIndex];
                    string fieldValue = string.Empty;

                    try
                    {
                        // Получаем значение поля по порядковому номеру
                        fieldValue = csv.GetField(colIndex);
                    }
                    catch
                    {
                        // Если поле отсутствует, оставляем пустым
                    }

                    if (string.IsNullOrWhiteSpace(fieldValue))
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
                    }
                    else
                    {
                        try
                        {
                            // Преобразуем значение в тип, соответствующий колонке
                            var typedValue = DataProcessingUtils.ConvertToTargetType(fieldValue, column.Type);
                            rowData[column.Name] = typedValue;
                        }
                        catch (Exception ex)
                        {
                            rowHasErrors = true;
                            importResult.ErrorCount++;
                            importResult.Errors.Add(new ImportError
                            {
                                RowNumber = rowIndex,
                                ErrorMessage =
                                    $"Ошибка в колонке '{column.Name}': {ex.Message}. Значение: '{fieldValue}'"
                            });
                        }
                    }
                }

                // Добавляем служебные поля
                rowData[DataProcessingUtils.MODIFIED_DATE_COLUMN] = DateTime.UtcNow;
                rowData[DataProcessingUtils.MODIFIED_BY_COLUMN] = userName;

                // Если нет ошибок, проверяем дубликаты (если это не новая таблица)
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

                // Пакетная обработка: при накоплении 1000 строк выполняем вставку в БД
                if (rowsToImport.Count >= 1000)
                {
                    // await ImportDataInParallelAsync(
                    //     importRequest.TableName,
                    //     rowsToImport,
                    //     new TableModel { Columns = importRequest.Columns, TableName = importRequest.TableName });
                    // rowsToImport.Clear();
                }
            }

            importResult.DuplicatedRows = duplicatedRows;

            // Импорт оставшихся строк
            if (rowsToImport.Count > 0)
            {
                await _dataImportRepository.ImportDataBatchAsync(
                    importRequest.TableName,
                    rowsToImport,
                    new TableModel { Columns = importRequest.Columns, TableName = importRequest.TableName });
            }

            importResult.RowsUpdated = importResult.RowsProcessed - importResult.RowsInserted -
                                       importResult.RowsSkipped - importResult.ErrorCount;
        }
        catch (Exception ex)
        {
            // Обработка ошибок: логирование и проброс исключения
            throw;
        }
    }
}