using Core.Models;
using Core.Results;
using MediatR;

namespace Core.Commands;

public class ImportDataCommand: IRequest<ImportResult>
{
    public Stream FileStream { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public TableImportRequestModel ImportRequest { get; }
    
    public ImportDataCommand(Stream fileStream, string fileName, string contentType, TableImportRequestModel importRequest)
    {
        FileStream = fileStream;
        FileName = fileName;
        ContentType = contentType;
        ImportRequest = importRequest;
    }
}