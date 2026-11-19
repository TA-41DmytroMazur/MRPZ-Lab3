using ClassroomManagement.Api.Data;
using ClassroomManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassroomManagement.Api.Repositories;

/// <summary>
/// Реалізація IBookingRepository через Entity Framework Core для PostgreSQL.
/// Виконує збереження бронювань та SQL-перевірку часових перетинів.
/// </summary>
public class EfBookingRepository : IBookingRepository
{
    private readonly AppDbContext _dbContext;

    public EfBookingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Booking> AddAsync(Booking booking)
    {
        if (booking.Id == Guid.Empty)
        {
            booking.Id = Guid.NewGuid();
        }

        // Приводимо всі дати до UTC — обов'язкова вимога Npgsql для timestamptz
        booking.StartTime = EnsureUtc(booking.StartTime);
        booking.EndTime = EnsureUtc(booking.EndTime);
        booking.CreatedAt = EnsureUtc(booking.CreatedAt);

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync(); // Фіксує INSERT у таблицю bookings
        return booking;
    }

    public async Task<Booking?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IEnumerable<Booking>> GetByClassroomIdAsync(Guid classroomId)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.ClassroomId == classroomId)
            .OrderBy(b => b.StartTime)
            .ToListAsync();
    }

    public async Task<bool> HasConflictAsync(Guid classroomId, DateTime start, DateTime end)
    {
        var startUtc = EnsureUtc(start);
        var endUtc = EnsureUtc(end);

        // Алгоритм перевірки перетинів часу (KHPT-18) транслюється EF Core у SQL EXISTS(...):
        // Два інтервали перетинаються, якщо Start1 < End2 && End1 > Start2 (суміжні слоти дозволені).
        return await _dbContext.Bookings
            .AsNoTracking()
            .AnyAsync(b =>
                b.ClassroomId == classroomId &&
                startUtc < b.EndTime &&
                endUtc > b.StartTime);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
