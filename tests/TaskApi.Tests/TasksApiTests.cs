using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TaskApi.Api.Dtos;
using TaskApi.Api.Models;

namespace TaskApi.Tests;

// Tests de integración: peticiones HTTP reales contra la API completa.
// Todos los tests de la clase comparten una base de datos, así que cada uno usa
// un responsable único (NewAssignee) para no depender de los datos de los demás.
public class TasksApiTests(TaskApiFactory factory) : IClassFixture<TaskApiFactory>
{
    private const string Url = "/api/tasks";
    private readonly HttpClient _client = factory.CreateClient();

    private static string NewAssignee() => $"user-{Guid.NewGuid():N}"[..20];

    private static object ValidTask(string assignee, string title = "Write README",
        string? status = null, string dueDate = "2026-12-01T10:00:00Z") =>
        new { title, description = "Docs", assignee, status, dueDate };

    private async Task<TaskResponse> CreateAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TaskResponse>(TaskApiFactory.Json))!;
    }

    // ---------- Crear ----------

    [Fact]
    public async Task Create_ValidTask_Returns201_WithLocationThatResolvesToTheTask()
    {
        var assignee = NewAssignee();

        var response = await _client.PostAsJsonAsync(Url, ValidTask(assignee));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TaskResponse>(TaskApiFactory.Json);
        Assert.NotNull(created);
        Assert.Equal(TaskItemStatus.Pending, created.Status); // estado por defecto
        Assert.Equal(TaskApiFactory.Now.UtcDateTime, created.CreatedAt);

        // La cabecera Location apunta a la tarea recién creada.
        Assert.NotNull(response.Headers.Location);
        var fetched = await _client.GetFromJsonAsync<TaskResponse>(response.Headers.Location, TaskApiFactory.Json);
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Create_WithMissingFields_Returns400_WithAnErrorPerField()
    {
        var response = await _client.PostAsJsonAsync(Url, new { title = "   ", description = "no title" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Title", problem.Errors.Keys);
        Assert.Contains("Assignee", problem.Errors.Keys);
        Assert.Contains("DueDate", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("2026-11-20T23:59:59Z", HttpStatusCode.BadRequest)] // ayer
    [InlineData("2026-11-21T00:00:00Z", HttpStatusCode.Created)]    // hoy: permitido
    public async Task Create_DueDate_CannotBeInThePast(string dueDate, HttpStatusCode expected)
    {
        var response = await _client.PostAsJsonAsync(Url, ValidTask(NewAssignee(), dueDate: dueDate));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownStatus_Returns400()
    {
        var response = await _client.PostAsJsonAsync(Url, ValidTask(NewAssignee(), status: "Lost"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Ver / editar / borrar ----------

    [Fact]
    public async Task Get_UnknownId_Returns404_AsProblemDetails()
    {
        var response = await _client.GetAsync($"{Url}/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem?.Status);
    }

    [Fact]
    public async Task Update_ExistingTask_Returns200_AndPersistsTheChanges()
    {
        var assignee = NewAssignee();
        var created = await CreateAsync(ValidTask(assignee));

        // Una fecha pasada está permitida al editar (para cerrar tareas vencidas).
        var response = await _client.PutAsJsonAsync($"{Url}/{created.Id}", new
        {
            title = "README final",
            assignee,
            status = "Done",
            dueDate = "2026-01-01T00:00:00Z"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await _client.GetFromJsonAsync<TaskResponse>($"{Url}/{created.Id}", TaskApiFactory.Json);
        Assert.Equal("README final", fetched?.Title);
        Assert.Equal(TaskItemStatus.Done, fetched?.Status);
        Assert.Null(fetched?.Description); // PUT reemplaza la tarea completa
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"{Url}/999999",
            new { title = "x", assignee = "x", status = "Done", dueDate = "2026-12-01" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingTask_Returns204_AndThenTheTaskIsGone()
    {
        var created = await CreateAsync(ValidTask(NewAssignee()));

        var delete = await _client.DeleteAsync($"{Url}/{created.Id}");
        var getAfter = await _client.GetAsync($"{Url}/{created.Id}");
        var deleteAgain = await _client.DeleteAsync($"{Url}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
    }

    // ---------- Listar: filtros y paginación ----------

    [Fact]
    public async Task List_FiltersByStatusAndAssignee_CaseInsensitive()
    {
        var assignee = NewAssignee();
        await CreateAsync(ValidTask(assignee, "A", status: "Done"));
        await CreateAsync(ValidTask(assignee, "B", status: "Pending"));
        await CreateAsync(ValidTask(assignee, "C", status: "Done"));
        await CreateAsync(ValidTask(NewAssignee(), "Other", status: "Done")); // otro responsable

        var page = await _client.GetFromJsonAsync<PagedResult<TaskResponse>>(
            $"{Url}?status=Done&assignee={assignee.ToUpperInvariant()}", TaskApiFactory.Json);

        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["A", "C"], page.Items.Select(t => t.Title).Order());
        Assert.All(page.Items, t => Assert.Equal(TaskItemStatus.Done, t.Status));
    }

    [Fact]
    public async Task List_Paginates_OrderedByDueDate_WithCorrectTotals()
    {
        var assignee = NewAssignee();
        for (var day = 1; day <= 5; day++)
            await CreateAsync(ValidTask(assignee, $"T{day}", dueDate: $"2026-12-{day:00}T00:00:00Z"));

        var page2 = await _client.GetFromJsonAsync<PagedResult<TaskResponse>>(
            $"{Url}?assignee={assignee}&page=2&pageSize=2", TaskApiFactory.Json);

        Assert.NotNull(page2);
        Assert.Equal(["T3", "T4"], page2.Items.Select(t => t.Title));
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(2, page2.Page);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=Unknown")]
    public async Task List_InvalidQuery_Returns400(string query)
    {
        var response = await _client.GetAsync($"{Url}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
