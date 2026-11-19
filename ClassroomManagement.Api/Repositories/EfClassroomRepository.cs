using ClassroomManagement.Api.Data;
using ClassroomManagement.Api.DTOs;
using ClassroomManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassroomManagement.Api.Repositories;

/// <summary>
/// Реалізація IClassroomRepository через Entity Framework Core для PostgreSQL.
/// Завдяки спільному інтерфейсу з ЛР №2 контролери та валідатори не потребували змін.
/// </summary>
public class EfClassroomRepository : IClassroomRepository
{
    private readonly AppDbContext _dbContext;

    public EfClassroomRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Classroom> AddAsync(Classroom classroom)
    {
        if (classroom.Id == Guid.Empty)
        {
            classroom.Id = Guid.NewGuid();
        }

        // PostgreSQL тип timestamptz вимагає DateTimeKind.Utc
        classroom.CreatedAt = EnsureUtc(classroom.CreatedAt);
        classroom.Equipment ??= new List<string>();

        _dbContext.Classrooms.Add(classroom);
        await _dbContext.SaveChangesAsync(); // Фіксує INSERT у PostgreSQL
        return classroom;
    }

    public async Task<Classroom?> GetByIdAsync(Guid id)
    {
        // AsNoTracking() прискорює операції читання, бо EF не відстежує зміни об'єкта
        return await _dbContext.Classrooms
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Classroom>> GetAllAsync()
    {
        return await _dbContext.Classrooms
            .AsNoTracking()
            .OrderBy(c => c.Building)
            .ThenBy(c => c.Number)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(string number, string building)
    {
        var normalizedNumber = number.Trim().ToLower();
        var normalizedBuilding = building.Trim().ToLower();

        // Перевірка дублікатів у БД без урахування регістру (SQL LOWER(...))
        return await _dbContext.Classrooms
            .AsNoTracking()
            .AnyAsync(c =>
                c.Number.ToLower() == normalizedNumber &&
                c.Building.ToLower() == normalizedBuilding);
    }

    public async Task<IEnumerable<Classroom>> SearchAsync(ClassroomSearchQuery query)
    {
        // Будуємо SQL-запит динамічно через IQueryable (фільтри виконуються на стороні PostgreSQL)
        IQueryable<Classroom> queryable = _dbContext.Classrooms.AsNoTracking();

        if (query.MinCapacity.HasValue)
        {
            queryable = queryable.Where(c => c.Capacity >= query.MinCapacity.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Building))
        {
            var normalizedBuilding = query.Building.Trim().ToLower();
            queryable = queryable.Where(c => c.Building.ToLower().Contains(normalizedBuilding));
        }

        var candidates = await queryable
            .OrderBy(c => c.Building)
            .ThenBy(c => c.Number)
            .ToListAsync();

        // Регістронезалежна фільтрація по масиву обладнання (підтримка списку через кому)
        if (!string.IsNullOrWhiteSpace(query.Equipment))
        {
            var requestedEquipments = query.Equipment
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var req in requestedEquipments)
            {
                candidates = candidates
                    .Where(c => c.Equipment.Any(e => e.Equals(req, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        return candidates;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
