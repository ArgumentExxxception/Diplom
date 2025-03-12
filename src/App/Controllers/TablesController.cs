using Core;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace App.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TabelsController: ControllerBase
{
    private readonly IDatabaseService _databaseService;

    public TabelsController(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }
    
    [HttpGet("public")]
    public async Task<ActionResult<List<string>>> GetPublicTables()
    {
        var tables = await _databaseService.GetPublicTablesAsync();
        return Ok(tables);
    }
    
    [HttpPost("create")]
    public async Task<IActionResult> CreateTable([FromBody] MappingRequest request)
    {
        try
        {
            if (request == null || request.MappedColumns == null || request.FileData == null)
            {
                return BadRequest("Некорректные данные.");
            }

            // await _tableService.CreateTableAsync(request);
            return Ok("Таблица успешно создана.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка при создании таблицы: {ex.Message}");
        }
    }
}