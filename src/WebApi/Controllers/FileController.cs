using System.Text.Json;
using Core;
using Core.Commands;
using Core.Models;
using Core.Queries;
using Core.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize]
public class FileController: ControllerBase
{
    private IMediator _mediator { get; set; }
    private IBackgroundTaskService _backgroundTaskService { get; set; }
    private IFileHandlerService _fileHandlerService { get; set; }
    
    public FileController(IFileHandlerService fileHandlerService, IMediator mediator, IBackgroundTaskService backgroundTaskService)
    {
        _fileHandlerService = fileHandlerService;
        _mediator = mediator;
        _backgroundTaskService = backgroundTaskService;
    }

    [HttpPost("update-duplicates")]
    public async Task<IActionResult> UpdateDuplicates(
        [FromForm] string tableName,
        [FromForm] string duplicates,
        [FromForm] string columns)
    {
        try
        {
            string userName = HttpContext.User.Identity?.Name;
            var columnInfoList = JsonConvert.DeserializeObject<List<ColumnInfo>>(columns);
            if (columnInfoList == null)
                return BadRequest(new { Message = "Не удалось десериализовать структуру колонок" });
            
            var duplicatesArray = JArray.Parse(duplicates);
            var duplicatesDictionary = duplicatesArray
                .Select(item => NormalizeJsonDictionary(item))
                .ToList();
            
            await _mediator.Send(new UpdateDuplicatesCommand(tableName, duplicatesDictionary, columnInfoList, userName));

            return Ok(new { Message = "Дубликаты успешно обновлены", TableName = tableName });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            return StatusCode(500, new { Message = "Произошла ошибка при обработке дубликатов", Error = ex.Message });
        }
    }

    [HttpPost("import")]
    [RequestSizeLimit(10_737_418_240)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_737_418_240)]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ImportResult>> ImportData([FromForm] IFormFile file, 
        [FromForm] string importRequestJson, 
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не был предоставлен или пуст");
        }

        TableImportRequestModel importRequest;
        try
        {
            importRequest = JsonSerializer.Deserialize<TableImportRequestModel>(
                importRequestJson, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return BadRequest("Некорректные параметры импорта");
        }

        if (file.Length > 50 * 1024 * 1024)
        {
            await using var stream = file.OpenReadStream();
    
            var command = new EnqueueImportCommand(
                file.FileName,
                file.Length,
                importRequest,
                stream,
                importRequest.UserEmail);

            var task = await _mediator.Send(command, cancellationToken);

            return Accepted(new { Message = "Импорт запущен в фоне", TaskId = task.Id });
        }
        else
        {
            await using var stream = file.OpenReadStream();
            var command = new ImportDataCommand(stream, file.FileName, file.ContentType, importRequest);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }
    
    [HttpPost("export")]
[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> ExportData([FromBody] TableExportRequestModel exportRequest, CancellationToken cancellationToken)
{
    try
    {
        if (exportRequest == null)
        {
            return BadRequest("Не получены параметры экспорта");
        }

        if (string.IsNullOrEmpty(exportRequest.TableName))
        {
            return BadRequest("Не указано имя таблицы");
        }

        if (exportRequest.MaxRows == 0)
        {
            var estimatedSize = await _mediator.Send(new GetExportDataSizeQuery(exportRequest.TableName, exportRequest.FilterCondition), cancellationToken);
            
            if (estimatedSize > 50 * 1024 * 1024)
            {
                var task = await _backgroundTaskService.EnqueueExportTaskAsync(
                    exportRequest,
                    exportRequest.UserEmail,
                    cancellationToken);
                
                return Accepted(new { Message = "Экспорт запущен в фоне", TaskId = task.Id });
            }
        }

        var command = new ExportDataCommand(exportRequest);
        var (result, fileStream) = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { Message = result.Message });
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();
        
        return File(fileBytes, result.ContentType, result.FileName);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { Message = $"Произошла ошибка при экспорте: {ex.Message}" });
    }
}
    
    private Dictionary<string, object?> NormalizeJsonDictionary(JToken item)
    {
        return item.Children<JProperty>()
            .ToDictionary(
                prop => prop.Name,
                prop => GetValueFromToken(prop.Value)
            );
    }
    
    private object? GetValueFromToken(JToken token)
    {
        return token.Type switch
        {
            JTokenType.Integer => token.ToObject<int>(),
            JTokenType.Float => token.ToObject<double>(),
            JTokenType.String => token.ToObject<string>(),
            JTokenType.Boolean => token.ToObject<bool>(),
            JTokenType.Date => token.ToObject<DateTime>(),
            JTokenType.Null => null,
            JTokenType.Object => token.ToObject<Dictionary<string, object>>(),
            JTokenType.Array => token.ToObject<List<object>>(),
            _ => token.ToString()
        };
    }
}