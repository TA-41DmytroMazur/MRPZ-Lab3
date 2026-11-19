# ==============================================================================
# Багатоетапна збірка (Multi-stage build) для ASP.NET Core 8 Web API.
# Перевага: важкий образ SDK (~800 МБ) використовується лише для компіляції,
# а у фінальний контейнер потрапляє тільки легкий ASP.NET Runtime (~200 МБ)
# без вихідного коду та інструментів збірки (менший розмір і вища безпека).
# ==============================================================================

# --- ЕТАП 1 (Build): компіляція та публікація проєкту ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Спочатку копіюємо лише .csproj і робимо restore, щоб задіяти кешування шарів Docker:
# якщо NuGet-пакети не змінювались, повторна збірка не качатиме їх заново.
COPY ["ClassroomManagement.Api/ClassroomManagement.Api.csproj", "ClassroomManagement.Api/"]
RUN dotnet restore "ClassroomManagement.Api/ClassroomManagement.Api.csproj"

# Копіюємо решту вихідного коду API та публікуємо оптимізовану Release-збірку
COPY ["ClassroomManagement.Api/", "ClassroomManagement.Api/"]
WORKDIR "/src/ClassroomManagement.Api"
RUN dotnet publish "ClassroomManagement.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- ЕТАП 2 (Final Runtime): запуск готового застосунку ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Web API слухає порт 8080 на всіх мережевих інтерфейсах всередині контейнера
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Копіюємо лише скомпільовані файли з першого етапу (build)
COPY --from=build /app/publish .

# Команда запуску сервера при старті контейнера
ENTRYPOINT ["dotnet", "ClassroomManagement.Api.dll"]
