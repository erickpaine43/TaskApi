using Microsoft.EntityFrameworkCore;
using TaskApi.Api.Models;

namespace TaskApi.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<TaskItem>();

        task.ToTable("Tasks");
        task.Property(t => t.Title).IsRequired().HasMaxLength(200);
        task.Property(t => t.Description).HasMaxLength(2000);
        task.Property(t => t.Assignee).IsRequired().HasMaxLength(100);

        // Guardamos el enum como texto: la BD se lee sin tabla de equivalencias
        // y no se rompe si cambia el orden del enum.
        task.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        // Índices en los campos por los que filtramos.
        task.HasIndex(t => t.Status);
        task.HasIndex(t => t.Assignee);
    }
}
