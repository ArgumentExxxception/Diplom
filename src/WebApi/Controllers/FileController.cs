using System.Text.Json;
using Core;
using Core.Commands;
using Core.Models;
using Core.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FileController: ControllerBase
{
    private IMediator _mediator { get; set; }
    private IFileHandlerService _fileHandlerService { get; set; }
    
    public FileController(IFileHandlerService fileHandlerService, IMediator mediator)
    {
        _fileHandlerService = fileHandlerService;
        _mediator = mediator;
    }

    [HttpPost("update-duplicates")]
    public async Task<IActionResult> UpdateDuplicates([FromForm] string tableName, [FromForm] string duplicates)
    {
        try
        {
            // Десериализуем данные о дубликатах
            var duplicatesDictionary = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(duplicates);

            if (duplicatesDictionary is null || duplicatesDictionary.Count == 0)
                throw new JsonException("Не удалось десериализовать дубликаты");

            // await _fileHandlerService.UpdateDublicates(tableName, duplicatesDictionary);

            // Возвращаем успешный ответ
            return Ok(new { Message = "Дубликаты успешно обработаны", TableName = tableName });
        }
        catch (Exception ex)
        {
            // Логирование ошибки
            Console.WriteLine($"Ошибка: {ex.Message}");
            return StatusCode(500, new { Message = "Произошла ошибка при обработке дубликатов", Error = ex.Message });
        }
    }

    [HttpPost("import")]
    [RequestSizeLimit(1_073_741_824)] 
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ImportResult>> ImportData(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest("Ожидается multipart/form-data запрос");
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не был предоставлен или пуст");
        }

        if (!form.TryGetValue("importRequest", out var importRequestValues) || importRequestValues.Count == 0)
        {
            return BadRequest("Параметры импорта не были предоставлены");
        }

        TableImportRequestModel importRequest;
        try
        {
            importRequest = JsonSerializer.Deserialize<TableImportRequestModel>(
                                importRequestValues.ToString(), 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? 
                            throw new JsonException();
        }
        catch (JsonException)
        {
            return BadRequest("Некорректные параметры импорта");
        }

        using var stream = file.OpenReadStream();
        var command = new ImportDataCommand(stream, file.FileName, file.ContentType, importRequest);
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }
}