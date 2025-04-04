using Core.Commands;
using Core.Models;
using Core.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TabelsController: ControllerBase
{
    private readonly IMediator _mediator;

    public TabelsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpGet("public")]
    public async Task<ActionResult<List<TableModel>>> GetPublicTables()
    {
        var tables = await _mediator.Send(new GetPublicTablesQuery());
        return Ok(tables);
    }
    
    [HttpPost("create")]
    public async Task<IActionResult> CreateTable([FromBody] TableModel request)
    {
        try
        {
            var result = await _mediator.Send(new CreateTableCommand(request));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка при создании таблицы: {ex.Message}");
        }
    }
}