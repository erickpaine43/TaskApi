using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace TaskApi.Tests;

/// <summary>
/// Levanta la API real en memoria (pipeline, validación, EF Core y migraciones incluidos)
/// contra una base SQLite en memoria, y con un reloj fijo para que los tests de fechas
/// no dependan del día en que se ejecutan.
/// </summary>
public class TaskApiFactory : WebApplicationFactory<Program>
{
    // "Hoy" para todos los tests.
    public static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Nombre único por factory: cada clase de tests tiene su propia base de datos.
    private readonly string _connectionString =
        $"Data Source=tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    // Una base SQLite en memoria desaparece cuando se cierra la última conexión;
    // esta conexión queda abierta mientras viva la factory para conservar los datos.
    private readonly SqliteConnection _keepAlive;

    public TaskApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}
