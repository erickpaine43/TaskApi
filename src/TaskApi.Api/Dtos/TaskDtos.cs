using System.ComponentModel.DataAnnotations;
using TaskApi.Api.Models;

namespace TaskApi.Api.Dtos;

// DTOs separados de la entidad: el cliente no puede tocar Id, CreatedAt ni UpdatedAt,
// y podemos cambiar la tabla sin romper el contrato de la API.
//
// Validación: con [ApiController], si alguna regla falla se devuelve 400 con
// ValidationProblemDetails (lista de errores por campo) sin llegar al controlador.
// [Required] en string también rechaza "" y "   ".
// DueDate y Status son nullable + [Required] para distinguir "no enviado" de un valor
// por defecto (0001-01-01 o Pending), que si no pasaría la validación en silencio.

public class CreateTaskRequest : IValidatableObject
{
    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    // Opcional al crear: si no se envía, la tarea empieza como Pending.
    public TaskItemStatus? Status { get; init; }

    [Required, StringLength(100)]
    public string Assignee { get; init; } = string.Empty;

    [Required]
    public DateTime? DueDate { get; init; }

    // Regla de negocio: no se puede crear una tarea que ya vence en el pasado.
    // Se compara por día (UTC) para permitir tareas que vencen hoy.
    // Al editar no se aplica: debe poder cerrarse una tarea ya vencida.
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DueDate is not { } dueDate) yield break;

        var clock = context.GetService<TimeProvider>() ?? TimeProvider.System;
        var today = clock.GetUtcNow().UtcDateTime.Date;

        if (DateTimeUtc.Normalize(dueDate).Date < today)
            yield return new ValidationResult(
                "The due date cannot be in the past.", [nameof(DueDate)]);
    }
}

// PUT reemplaza la tarea completa, así que todos los campos editables son obligatorios.
public class UpdateTaskRequest
{
    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Required]
    public TaskItemStatus? Status { get; init; }

    [Required, StringLength(100)]
    public string Assignee { get; init; } = string.Empty;

    [Required]
    public DateTime? DueDate { get; init; }
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
