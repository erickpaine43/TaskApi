using Microsoft.AspNetCore.Mvc;
using TaskApi.Api.Dtos;
using TaskApi.Api.Services;

namespace TaskApi.Api.Controllers;

[ApiController]
[Route("api/tasks")]
// Sin [Produces("application/json")]: forzaba ese Content-Type también en los errores,
// que deben salir como "application/problem+json" (lo detectó un test).
public class TasksController(ITaskService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<TaskResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TaskResponse>>> List([FromQuery] TaskQuery query, CancellationToken ct)
        => Ok(await service.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetById(int id, CancellationToken ct)
    {
        var task = await service.GetAsync(id, ct);
        return task is null ? TaskNotFound(id) : Ok(task);
    }

    [HttpPost]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var task = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> Update(int id, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await service.UpdateAsync(id, request, ct);
        return task is null ? TaskNotFound(id) : Ok(task);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await service.DeleteAsync(id, ct) ? NoContent() : TaskNotFound(id);

    // 404 en ProblemDetails con un mensaje que dice qué no se encontró.
    private ObjectResult TaskNotFound(int id) =>
        Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Task not found",
                detail: $"No task exists with id {id}.");
}
