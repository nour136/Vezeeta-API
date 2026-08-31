# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first so NuGet restore is cached across builds
COPY ["Vezeeta.API/Vezeeta.API.csproj", "Vezeeta.API/"]
COPY ["Service/Service.csproj", "Service/"]
COPY ["Repository/Repository.csproj", "Repository/"]
COPY ["Domain/Domain.csproj", "Domain/"]

RUN dotnet restore "Vezeeta.API/Vezeeta.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR /src/Vezeeta.API
RUN dotnet build "Vezeeta.API.csproj" -c Release -o /app/build

# ---- Publish stage ----
FROM build AS publish
RUN dotnet publish "Vezeeta.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- Final runtime stage ----
# .NET 8's aspnet image listens on 8080 by default (not 80, as in .NET 6/7)
# so the app stays on the non-root 'app' user without extra config.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Vezeeta.API.dll"]