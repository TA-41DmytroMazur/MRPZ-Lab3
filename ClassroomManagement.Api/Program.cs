using ClassroomManagement.Api.Data;
using ClassroomManagement.Api.Repositories;
using ClassroomManagement.Api.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Підключення контролерів та кастомізація відповідей валідації (дружні повідомлення українською)
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = new Dictionary<string, string[]>();

            foreach (var entry in context.ModelState)
            {
                if (entry.Value.Errors.Count == 0)
                {
                    continue;
                }

                // Прибираємо вторинну технічну помилку "dto is required", якщо вже є помилка конкретного поля
                if (entry.Key == "dto" && context.ModelState.Count > 1)
                {
                    continue;
                }

                var cleanKey = entry.Key.StartsWith("$.") ? entry.Key[2..] : entry.Key;
                var messages = entry.Value.Errors
                    .Select(err => MapFriendlyValidationMessage(cleanKey, err.ErrorMessage))
                    .ToArray();

                errors[cleanKey] = messages;
            }

            return new BadRequestObjectResult(new
            {
                message = "Помилка валідації вхідних даних.",
                errors
            });
        };
    });

// 2. Генерація інтерактивної документації OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "University Classroom Management Web API",
        Version = "v1",
        Description = "API для керування університетськими аудиторіями та їх бронюванням (PostgreSQL + Docker Compose, ЛР №3 з МРПЗ)"
    });
});

// 3. Зчитуємо рядок підключення до PostgreSQL (у Docker передається з .env через ConnectionStrings__DefaultConnection)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var usePostgres = !string.IsNullOrWhiteSpace(connectionString);

if (usePostgres)
{
    // Підключаємо EF Core із драйвером Npgsql для PostgreSQL
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // ВАЖЛИВО: для EF Core використовується AddScoped (один DbContext на один HTTP-запит),
    // на відміну від AddSingleton, який був у ЛР №2 для In-Memory сховища.
    builder.Services.AddScoped<IClassroomRepository, EfClassroomRepository>();
    builder.Services.AddScoped<ClassroomValidator>();

    builder.Services.AddScoped<IBookingRepository, EfBookingRepository>();
    builder.Services.AddScoped<BookingValidator>();
}
else
{
    // Резервний режим для локального запуску без PostgreSQL (In-Memory)
    builder.Services.AddSingleton<IClassroomRepository, InMemoryClassroomRepository>();
    builder.Services.AddSingleton<ClassroomValidator>();

    builder.Services.AddSingleton<IBookingRepository, InMemoryBookingRepository>();
    builder.Services.AddSingleton<BookingValidator>();
}

var app = builder.Build();

// 4. Автоматичне застосування міграцій EF Core при запуску контейнера (створює таблиці classrooms та bookings)
if (usePostgres)
{
    await ApplyMigrationsWithRetryAsync(app.Services, app.Logger);
}

// 5. Налаштування HTTP-конвеєра: Swagger доступний у Development або при EnableSwagger=true
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Службовий ендпоінт для швидкої перевірки стану сервера та активного сховища (http://localhost:8080/health)
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    database = usePostgres ? "PostgreSQL" : "InMemory"
}));

app.Run();

// Механізм повторних спроб (retry), якщо контейнер PostgreSQL ще завершує ініціалізацію
static async Task ApplyMigrationsWithRetryAsync(IServiceProvider services, ILogger logger)
{
    const int maxRetries = 10;

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("PostgreSQL migrations applied successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex,
                "Failed to connect to PostgreSQL (attempt {Attempt}/{MaxRetries}). Retrying in 2 seconds...",
                attempt, maxRetries);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    using var finalScope = services.CreateScope();
    var finalDbContext = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await finalDbContext.Database.MigrateAsync();
}

static string MapFriendlyValidationMessage(string field, string rawMessage)
{
    if (rawMessage.Contains("System.Guid", StringComparison.OrdinalIgnoreCase))
    {
        return $"Поле '{field}' має невірний формат GUID (очікується формат xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).";
    }

    if (rawMessage.Contains("System.DateTime", StringComparison.OrdinalIgnoreCase))
    {
        return $"Поле '{field}' має невірний формат дати й часу (очікується ISO 8601, наприклад 2026-11-18T10:00:00Z).";
    }

    return string.IsNullOrWhiteSpace(rawMessage)
        ? $"Некоректне значення поля '{field}'."
        : rawMessage;
}
