# TaskApi

API REST para gestionar tareas, hecha con ASP.NET Core 10 (C#), EF Core y SQLite.

Cada tarea tiene título, descripción, estado (`Pending`, `InProgress`, `Done`), responsable y fecha límite. La API permite crear, listar, ver, editar y borrar tareas, y filtrar por estado y responsable con paginación.

## Cómo ejecutarlo

**Requisito:** [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). No hace falta instalar ninguna base de datos: SQLite va incluido como librería.

```bash
git clone <url-del-repo>
cd TaskApi
dotnet run --project src/TaskApi.Api
```

La API queda en `http://localhost:5228`. Al arrancar se aplican las migraciones y se crea el archivo `src/TaskApi.Api/tasks.db`.

- Especificación OpenAPI (en entorno Development): `http://localhost:5228/openapi/v1.json`
- Peticiones de ejemplo listas para usar: [`src/TaskApi.Api/TaskApi.Api.http`](src/TaskApi.Api/TaskApi.Api.http)

**Tests:**

```bash
dotnet test
```

## Endpoints

| Método | Ruta | Respuestas |
|---|---|---|
| `GET` | `/api/tasks?status=&assignee=&page=1&pageSize=10` | 200, 400 |
| `GET` | `/api/tasks/{id}` | 200, 404 |
| `POST` | `/api/tasks` | 201 (con cabecera `Location`), 400 |
| `PUT` | `/api/tasks/{id}` | 200, 400, 404 |
| `DELETE` | `/api/tasks/{id}` | 204, 404 |

Ejemplo:

```bash
curl -X POST http://localhost:5228/api/tasks \
  -H "Content-Type: application/json" \
  -d '{"title":"Preparar la demo","assignee":"ana","dueDate":"2026-12-15T17:00:00Z"}'
```

```json
{
  "id": 1,
  "title": "Preparar la demo",
  "description": null,
  "status": "Pending",
  "assignee": "ana",
  "dueDate": "2026-12-15T17:00:00Z",
  "createdAt": "2026-11-21T10:00:00Z",
  "updatedAt": "2026-11-21T10:00:00Z"
}
```

El listado devuelve `{ items, page, pageSize, totalCount, totalPages }`.

### Reglas de validación

- `title`: obligatorio, máximo 200 caracteres (no vale vacío ni solo espacios).
- `assignee`: obligatorio, máximo 100 caracteres.
- `description`: opcional, máximo 2000 caracteres.
- `status`: solo `Pending`, `InProgress` o `Done` (como texto). Opcional al crear (por defecto `Pending`), obligatorio al editar.
- `dueDate`: obligatoria. Al **crear** no puede ser anterior a hoy (UTC); al **editar** sí, para poder cerrar tareas ya vencidas.
- Listado: `page` entre 1 y 1.000.000, `pageSize` entre 1 y 100.

### Errores

Todos los errores siguen el estándar ProblemDetails ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)) con `Content-Type: application/problem+json`. Los errores de validación indican qué campo falla:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Title": ["The Title field is required."],
    "DueDate": ["The due date cannot be in the past."]
  }
}
```

| Código | Cuándo |
|---|---|
| 400 | Datos no válidos, JSON mal formado, parámetros de consulta fuera de rango |
| 404 | La tarea no existe |
| 405 | Método no permitido en esa ruta |
| 415 | Falta `Content-Type: application/json` |
| 500 | Error no controlado (sin exponer detalles internos) |

## Estructura

```
src/TaskApi.Api/
  Controllers/   TasksController: solo traduce entre HTTP y el servicio
  Services/      TaskService: lógica de negocio y consultas
  Dtos/          Contratos de entrada/salida, validación y paginación
  Models/        Entidad TaskItem y enum de estado
  Data/          AppDbContext (configuración de EF Core)
  Migrations/    InitialCreate y AssigneeCaseInsensitive
tests/TaskApi.Tests/
  TaskApiFactory  Levanta la API real con SQLite en memoria y un reloj fijo
  TasksApiTests   15 tests de integración
```

## Decisiones

- **SQLite.** Quien clone el repo lo ejecuta con un solo comando, sin instalar ni configurar un servidor de base de datos. Pasar a PostgreSQL o SQL Server solo requiere cambiar el proveedor de EF Core y la cadena de conexión.
- **Migraciones aplicadas al arrancar.** Cómodo para una prueba y para desarrollo. En producción las aplicaría en el despliegue (bundle de migraciones o script SQL), no al iniciar cada instancia.
- **DTOs separados de la entidad.** El cliente no puede tocar `Id`, `CreatedAt` ni `UpdatedAt`, y la tabla puede cambiar sin romper el contrato de la API.
- **Capa de servicio fina, sin repositorio.** El `DbContext` ya funciona como unidad de trabajo y repositorio; añadir otra capa encima no aportaba nada en un proyecto de este tamaño.
- **Validación con DataAnnotations e `IValidatableObject`.** Es lo que trae ASP.NET Core de serie y, con `[ApiController]`, devuelve 400 automáticamente. `Status` y `DueDate` son *nullable* con `[Required]` para distinguir "no enviado" de un valor por defecto (`0001-01-01` o `Pending`), que si no pasaría en silencio.
- **Estado guardado como texto** en la base de datos y en el JSON, y solo se aceptan nombres (`"status": 7` da 400). La base de datos se entiende sin tabla de equivalencias y no se rompe si cambia el orden del enum.
- **Fechas siempre en UTC.** Si una fecha llega sin zona horaria, se interpreta como UTC.
- **`TimeProvider` inyectado** en lugar de `DateTime.UtcNow`, para que los tests de la regla "fecha no en el pasado" no dependan del día en que se ejecutan.
- **Paginación con orden estable** (fecha límite y luego `Id`) para que las páginas no repitan ni salten tareas. Hay índices en `Status` y `Assignee`, los campos por los que se filtra.
- **Filtro por responsable sin distinguir mayúsculas**, con la intercalación `NOCASE` de SQLite en esa columna (una segunda migración). Limitación conocida: en SQLite solo cubre ASCII (`Ángel` y `ángel` no coinciden).
- **PUT reemplaza la tarea completa.** Es su semántica en HTTP; para cambios parciales añadiría `PATCH`.
- **Tests de integración** con `WebApplicationFactory` en lugar de tests unitarios con mocks: prueban la API de verdad (enrutado, validación, serialización, EF Core y migraciones) y siguen siendo rápidos (unos 2 segundos).

## Qué haría con más tiempo

- Autenticación y autorización (JWT), y que el responsable sea un usuario real en lugar de un texto libre.
- `PATCH` para cambiar solo el estado sin reenviar toda la tarea.
- Control de concurrencia optimista (`ETag` / `If-Match`) para que dos ediciones simultáneas no se pisen.
- Paginación por cursor, porque `Skip` se vuelve lento con muchas filas.
- Más filtros y orden configurable (por fecha límite, tareas vencidas, búsqueda por título).
- Interfaz visual de la documentación (Scalar o Swagger UI) sobre el OpenAPI que ya se genera.
- Dockerfile y PostgreSQL con Testcontainers en los tests.
- CI con GitHub Actions (build + tests en cada push).
- Logs estructurados, health checks y versionado de la API.
- Mensajes de error en español (ahora son los que trae ASP.NET Core, en inglés).

## Cómo usé la IA

Usé **Claude (Claude Code)** como asistente durante toda la prueba.

**Qué le pedí:**
- Un plan paso a paso a partir del enunciado. Lo seguí tal cual: un paso por commit.
- Los primeros pasos los hice yo con sus instrucciones: instalar el SDK y crear la solución.
- A partir del modelo de datos, le pedí que escribiera el código de cada paso bajo mi dirección, que lo probara (con `curl` y después con los tests) y que hiciera el commit al terminar cada paso, que yo revisaba antes de pasar al siguiente.

**Qué acepté:**
- La arquitectura (controlador fino, servicio, DTOs) y las decisiones descritas arriba.
- Añadir más tests de los 3 mínimos para cubrir los casos de error, no solo el camino feliz.

**Qué se corrigió por el camino:**
- **Instalación del SDK:** el canal `10.0` de snap que me indicó no existe; lo instalé con `apt`.
- **Versiones de paquetes:** `dotnet add package` instalaba EF Core 11, que no es compatible con .NET 10. Se fijó la versión 10.0.15.
- **Errores de JSON:** la primera versión devolvía mensajes con nombres internos de .NET (`System.Nullable`1[...]`) y un error duplicado y confuso, `"The request field is required"`. Se vio al probar a mano y se corrigió.
- **Content-Type de los errores:** el atributo `[Produces("application/json")]` del controlador hacía que los errores salieran como `application/json` en lugar de `application/problem+json`. Lo detectó un test y se corrigió en su propio commit.
- **Pruebas manuales:** una prueba del PUT daba un 404 falso porque usaba una tarea ya borrada; se repitió con una que existía.
- **Historial:** un commit mezclaba el arreglo anterior con el borrado del test de ejemplo; se rehízo (antes de publicar) para que cada commit tuviera solo lo suyo.
- **Pendiente:** no conseguimos provocar un error 500 real en ejecución para comprobarlo (SQLite ya tenía el archivo abierto). La configuración es la estándar de ASP.NET Core.

<!-- TODO Erick: añade aquí, con tus palabras, qué revisaste o cambiaste tú mismo y qué opinas del resultado. Borra este comentario antes de entregar. -->

## Tiempo invertido

<!-- TODO Erick: pon el tiempo real total. Borra este comentario antes de entregar. -->
Aproximadamente **X horas**.
