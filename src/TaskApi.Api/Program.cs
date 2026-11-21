using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskApi.Api.Data;
using TaskApi.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    // Estados como texto en el JSON ("Pending", "InProgress", "Done") en vez de 0, 1, 2.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITaskService, TaskService>();

var app = builder.Build();

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
