using Microsoft.EntityFrameworkCore;
using TaskApi.Api.Data;
using TaskApi.Api.Dtos;
using TaskApi.Api.Models;

namespace TaskApi.Api.Services;

// TimeProvider se inyecta en vez de usar DateTime.UtcNow para poder fijar la hora en los tests.
public class TaskService(AppDbContext db, TimeProvider clock) : ITaskService
{
    public async Task<IReadOnlyList<TaskResponse>> ListAsync(CancellationToken ct)
    {
        var tasks = await db.Tasks
            .AsNoTracking()
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        return tasks.Select(ToResponse).ToList();
    }

    public async Task<TaskResponse?> GetAsync(int id, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        return task is null ? null : ToResponse(task);
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = request.Status ?? TaskItemStatus.Pending,
            Assignee = request.Assignee.Trim(),
            DueDate = ToUtc(request.DueDate),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        return ToResponse(task);
    }

    public async Task<TaskResponse?> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return null;

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Status = request.Status;
        task.Assignee = request.Assignee.Trim();
        task.DueDate = ToUtc(request.DueDate);
        task.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        return ToResponse(task);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        // Borrado directo en BD, sin cargar la entidad primero.
        var deleted = await db.Tasks.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    // Todas las fechas se guardan en UTC. Si llegan sin zona horaria, se asumen UTC.
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    // SQLite devuelve las fechas sin "Kind"; las marcamos como UTC para que el JSON lleve la "Z".
    private static TaskResponse ToResponse(TaskItem t) => new(
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.Assignee,
        DateTime.SpecifyKind(t.DueDate, DateTimeKind.Utc),
        DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc),
        DateTime.SpecifyKind(t.UpdatedAt, DateTimeKind.Utc));
}
