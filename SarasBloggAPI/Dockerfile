# 1. Byggfas
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopiera och återställ beroenden
COPY ["SarasBloggAPI.csproj", "./"]
RUN dotnet restore "SarasBloggAPI.csproj"

# Kopiera all kod och bygg
COPY . .
RUN dotnet build "SarasBloggAPI.csproj" -c Release -o /app/build

# Publiceringsfas
FROM build AS publish
RUN dotnet publish "SarasBloggAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SarasBloggAPI.dll"]