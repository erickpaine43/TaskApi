using TaskApi.Api.Dtos;

namespace TaskApi.Api.Services;

public interface ITaskService
{
    Task<PagedResult<TaskResponse>> ListAsync(TaskQuery query, CancellationToken ct);
    Task<TaskResponse?> GetAsync(int id, CancellationToken ct);
    Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken ct);

    /// <returns>La tarea actualizada, o null si no existe.</returns>
    Task<TaskResponse?> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken ct);

    /// <returns>true si se borró, false si no existía.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
