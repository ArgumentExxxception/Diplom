using Core;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

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
    public async Task<ActionResult<List<TableModel>>> GetPublicTables()
    {
        var tables = await _databaseService.GetPublicTablesAsync();
        return Ok(tables);
    }
    
    [HttpPost("create")]
    public async Task<IActionResult> CreateTable([FromBody] TableModel request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.TableName))
            {
                return BadRequest("Некорректные данные.");
            }
            
            await _databaseService.CreateTableAsync(request);
            return Ok("Таблица успешно создана.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка при создании таблицы: {ex.Message}");
        }
    }
}