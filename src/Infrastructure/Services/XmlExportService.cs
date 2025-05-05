using System.Text;
using System.Xml;
using Core.Models;
using Core.RepoInterfaces;
using Core.Results;
using Core.ServiceInterfaces;

namespace Infrastructure.Services;

public class XmlExportService: IXmlExportService
{
private readonly IDataExportRepository _dataExportRepository;

    public XmlExportService(IDataExportRepository dataExportRepository)
    {
        _dataExportRepository = dataExportRepository;
    }

    public async Task<ExportResult> ExportToXmlAsync(
        TableExportRequestModel exportRequest,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ExportResult
        {
            TableName = exportRequest.TableName,
            ExportFormat = "XML",
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
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = encoding,
                CloseOutput = false,
                Async = true
            };

            await using (var writer = XmlWriter.Create(outputStream, settings))
            {
                await writer.WriteStartDocumentAsync();
                writer.WriteStartElement(exportRequest.XmlRootElement ?? "root");

                writer.WriteStartElement("metadata");
                writer.WriteElementString("exportDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.WriteElementString("tableName", exportRequest.TableName);
                writer.WriteElementString("rowCount", rows.Count.ToString());
                await writer.WriteEndElementAsync();
                
                writer.WriteStartElement("data");
                foreach (var row in rows)
                {
                    writer.WriteStartElement(exportRequest.XmlRowElement ?? "row");

                    foreach (var column in columns)
                    {
                        writer.WriteStartElement(NormalizeXmlElementName(column.Name));

                        if (row.TryGetValue(column.Name, out var value))
                        {
                            if (value != null)
                            {
                                switch (value)
                                {
                                    case DateTime dateTime:
                                        await writer.WriteStringAsync(dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                                        break;
                                    case bool boolean:
                                        await writer.WriteStringAsync(boolean ? "true" : "false");
                                        break;
                                    default:
                                        await writer.WriteStringAsync(value.ToString());
                                        break;
                                }
                            }
                        }

                        await writer.WriteEndElementAsync();
                    }

                    await writer.WriteEndElementAsync();
                }

                await writer.WriteEndElementAsync();

                await writer.WriteEndElementAsync();
                await writer.WriteEndDocumentAsync();
                await writer.FlushAsync();
            }

            outputStream.Position = 0;

            result.Success = true;
            result.RowsExported = rows.Count;
            result.FileName = $"{exportRequest.TableName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xml";
            result.ContentType = "application/xml";
            result.FileSize = outputStream.Length;
            result.Message = $"Экспортировано {rows.Count} строк";
            result.ElapsedTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Ошибка при экспорте в XML: {ex.Message}";
            return result;
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedTimeMs = stopwatch.ElapsedMilliseconds;
        }
    }
    
    private string NormalizeXmlElementName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "item";
        
        var result = new StringBuilder();
        if (!char.IsLetter(name[0]) && name[0] != '_')
            result.Append('_');
        
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                result.Append(c);
            else
                result.Append('_');
        }

        return result.ToString();
    }
}