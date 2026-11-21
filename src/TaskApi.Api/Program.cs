using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskApi.Api.Data;
using TaskApi.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    // Evita el error duplicado "The request field is required" cuando el JSON no se puede leer:
    // las reglas obligatorias ya están declaradas con [Required] en los DTOs.
    .AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(o =>
    {
        // Estados como texto en el JSON ("Pending", "InProgress", "Done").
        // allowIntegerValues: false -> "status": 7 da 400 en vez de guardar un estado inexistente.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));

        // No exponer detalles internos (tipos de .NET) en los errores de JSON mal formado.
        o.AllowInputFormatterExceptionMessages = false;
    });
builder.Services.AddOpenApi();

// Todos los errores (400, 404, 500...) salen con el formato estándar ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITaskService, TaskService>();

var app = builder.Build();

// Excepciones no controladas -> 500 en ProblemDetails, sin exponer el stack trace.
app.UseExceptionHandler();
// Respuestas de error sin cuerpo (p. ej. 405, 415) -> también ProblemDetails.
app.UseStatusCodePages();

// Aplica las migraciones al arrancar: quien clone el repo solo necesita "dotnet run".
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necesario para que los tests de integración puedan usar WebApplicationFactory<Program>.
public partial class Program { }
