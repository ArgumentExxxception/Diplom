using Core;
using Core.Models;
using Core.Results;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;
using JsonSerializerOptions = System.Text.Json.JsonSerializerOptions;

namespace App.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController: ControllerBase
{
    private IFileHandlerService _fileHandlerService { get; set; }
    
    public FileController(IFileHandlerService fileHandlerService)
    {
        _fileHandlerService = fileHandlerService;
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

            await _fileHandlerService.UpdateDublicates(tableName, duplicatesDictionary);

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
    public async Task<ActionResult<ImportResult>> ImportData()
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest("Ожидается multipart/form-data запрос");
        }

        var form = await Request.ReadFormAsync();
    
        // Получение и проверка файла
        var file = form.Files.GetFile("file");
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не был предоставлен или пуст");
        }

        // Получение и проверка параметров импорта
        if (!form.TryGetValue("importRequest", out var importRequestValues) || importRequestValues.Count == 0)
        {
            return BadRequest("Параметры импорта не были предоставлены");
        }

        var importRequestJson = importRequestValues.ToString();
        
        // Десериализация параметров импорта
        TableImportRequestModel importRequestModel = new ();
        try
        {
            importRequestModel = JsonSerializer.Deserialize<TableImportRequestModel>(importRequestJson, 
                new JsonSerializerOptions
                { 
                    PropertyNameCaseInsensitive = true 
                });
            
            if (importRequestModel == null)
            {
                return BadRequest("Некорректные параметры импорта");
            }
        }
        catch (JsonException ex)
        {
            // _logger.LogError(ex, "Ошибка при разборе JSON параметров импорта");
            // return BadRequest($"Некорректный формат параметров импорта: {ex.Message}");
        }
        
        // Получение информации о пользователе
        var userName = User.Identity?.Name ?? "System";

        // Засекаем время начала операции
        var startTime = DateTime.Now;

        // Импорт данных через сервис из Infrastructure слоя
        using var stream = file.OpenReadStream();
        var result = await _fileHandlerService.ImportDataAsync(
            stream, 
            file.FileName, 
            file.ContentType,
            importRequestModel, 
            userName);
        
        // Добавляем в результат информацию о времени выполнения
        // result.ElapsedTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        //
        // // Логируем результат операции импорта
        // _logger.LogInformation(
        //     "Импорт завершен: Файл: {FileName}, Строк обработано: {RowCount}, Ошибок: {ErrorCount}, Время: {ElapsedTime}мс, Пользователь: {UserName}", 
        //     file.FileName, 
        //     result.ProcessedRowsCount,
        //     result.ErrorsCount,
        //     result.ElapsedTimeMs, 
        //     userName);

        return Ok(result);
    }
}