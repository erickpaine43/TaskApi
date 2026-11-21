using TaskApi.Api.Models;

namespace TaskApi.Api.Dtos;

// DTOs separados de la entidad: el cliente no puede tocar Id, CreatedAt ni UpdatedAt,
// y podemos cambiar la tabla sin romper el contrato de la API.

public class CreateTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    // Opcional al crear: si no se envía, la tarea empieza como Pending.
    public TaskItemStatus? Status { get; init; }

    public string Assignee { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
}

// PUT reemplaza la tarea completa, así que todos los campos editables son obligatorios.
public class UpdateTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public TaskItemStatus Status { get; init; }
    public string Assignee { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
}

public record TaskResponse(
    int Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    string Assignee,
    DateTime DueDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);
