using System.Globalization;
using System.Text;
using Core.Models;
using Core.RepoInterfaces;
using Core.Results;
using Core.ServiceInterfaces;
using CsvHelper;
using CsvHelper.Configuration;

namespace Infrastructure.Services;

public class CsvExportService: ICsvExportService
{
    private readonly IDataExportRepository _dataExportRepository;

    public CsvExportService(IDataExportRepository dataExportRepository)
    {
        _dataExportRepository = dataExportRepository;
    }

    public async Task<ExportResult> ExportToCsvAsync(
        TableExportRequestModel exportRequest,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ExportResult
        {
            TableName = exportRequest.TableName,
            ExportFormat = "CSV",
            Success = false
        };

        try
        {
            var (rows, columns) = await _dataExportRepository.GetDataForExportAsync(
                exportRequest.TableName,
                exportRequest.Columns,
                exportRequest.FilterCondition,
                exportRequest.MaxRows,
                cancellationToken);

            Encoding encoding = Encoding.UTF8;
            if (!string.IsNullOrEmpty(exportRequest.Encoding))
            {
                try
                {
                    encoding = Encoding.GetEncoding(exportRequest.Encoding);
                }
                catch (ArgumentException)
                {
                }
            }

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = exportRequest.Delimiter ?? ",",
                HasHeaderRecord = exportRequest.IncludeHeaders
            };

            await using (var writer = new StreamWriter(outputStream, encoding, leaveOpen: true))
            await using (var csv = new CsvWriter(writer, csvConfig))
            {
                if (exportRequest.IncludeHeaders)
                {
                    foreach (var column in columns)
                    {
                        csv.WriteField(column.Name);
                    }
                    await csv.NextRecordAsync();
                }
                
                foreach (var row in rows)
                {
                    foreach (var column in columns)
                    {
                        if (row.TryGetValue(column.Name, out var value))
                        {
                            switch (value)
                            {
                                case DateTime dateTime:
                                    csv.WriteField(dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                                    break;
                                case bool boolean:
                                    csv.WriteField(boolean ? "true" : "false");
                                    break;
                                default:
                                    csv.WriteField(value?.ToString());
                                    break;
                            }
                        }
                        else
                        {
                            csv.WriteField(string.Empty);
                        }
                    }
                    await csv.NextRecordAsync();
                }

                await csv.FlushAsync();
            }

            outputStream.Position = 0;
            
            result.Success = true;
            result.RowsExported = rows.Count;
            result.FileName = $"{exportRequest.TableName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            result.ContentType = "text/csv";
            result.FileSize = outputStream.Length;
            result.Message = $"Экспортировано {rows.Count} строк";
            result.ElapsedTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Ошибка при экспорте в CSV: {ex.Message}";
            return result;
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedTimeMs = stopwatch.ElapsedMilliseconds;
        }
    }
}