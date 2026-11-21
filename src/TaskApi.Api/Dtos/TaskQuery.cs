using System.ComponentModel.DataAnnotations;
using TaskApi.Api.Models;

namespace TaskApi.Api.Dtos;

// Parámetros de GET /api/tasks?status=Pending&assignee=ana&page=1&pageSize=10
// Con [ApiController], si algún valor no es válido (estado inexistente, page=0, pageSize=500)
// se devuelve 400 automáticamente antes de llegar al controlador.
public class TaskQuery
{
    public TaskItemStatus? Status { get; init; }

    [StringLength(100)]
    public string? Assignee { get; init; }

    // Límite superior para que (page - 1) * pageSize no desborde un int.
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
}
