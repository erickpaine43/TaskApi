namespace TaskApi.Api.Dtos;

public static class DateTimeUtc
{
    // Todas las fechas se guardan en UTC. Si llegan sin zona horaria, se asumen UTC.
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
