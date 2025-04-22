using System.Diagnostics;
using Core.Commands;
using Core.Models;
using Core.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TablesController: ControllerBase
{
    private readonly IMediator _mediator;

    public TablesController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpGet("public")]
    public async Task<ActionResult<List<TableModel>>> GetPublicTables()
    {
        var tables = await _mediator.Send(new GetPublicTablesQuery());
        foreach (var claim in User.Claims)
        {
            Debug.Print($"{claim.Type}: {claim.Value}");
        }
        return Ok(tables);
    }
}