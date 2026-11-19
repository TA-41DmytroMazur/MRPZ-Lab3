using ClassroomManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassroomManagement.Api.Data;

/// <summary>
/// Контекст бази даних Entity Framework Core для роботи з PostgreSQL (Npgsql).
/// Описує таблиці, обмеження цілісності, індекси та зовнішні ключі.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Кожен DbSet відповідає окремій таблиці в PostgreSQL
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Конфігурація таблиці "classrooms" (Аудиторії)
        modelBuilder.Entity<Classroom>(entity =>
        {
            entity.ToTable("classrooms");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Number)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(c => c.Building)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(c => c.Capacity)
                .IsRequired();

            // Npgsql нативно зберігає List<string> як PostgreSQL-масив text[]
            entity.Property(c => c.Equipment)
                .HasColumnType("text[]")
                .IsRequired();

            entity.Property(c => c.CreatedAt)
                .IsRequired();

            // Унікальний складений індекс: не можна мати 2 однакові номери аудиторій в одному корпусі
            entity.HasIndex(c => new { c.Number, c.Building })
                .IsUnique();
        });

        // Конфігурація таблиці "bookings" (Бронювання)
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings");
            entity.HasKey(b => b.Id);

            entity.Property(b => b.ClassroomId)
                .IsRequired();

            entity.Property(b => b.ReservedBy)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(b => b.StartTime)
                .IsRequired();

            entity.Property(b => b.EndTime)
                .IsRequired();

            entity.Property(b => b.CreatedAt)
                .IsRequired();

            // Зовнішній ключ на таблицю classrooms: при видаленні аудиторії каскадно видаляються її броні
            entity.HasOne<Classroom>()
                .WithMany()
                .HasForeignKey(b => b.ClassroomId)
                .OnDelete(DeleteBehavior.Cascade);

            // Складений індекс для швидкого пошуку перетинів часових інтервалів у PostgreSQL
            entity.HasIndex(b => new { b.ClassroomId, b.StartTime, b.EndTime });
        });
    }
}
