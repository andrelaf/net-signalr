# Build a partir da raiz do repositório:  docker build -f src/SignalRDemo.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (camadas cacheáveis): copia apenas os csproj primeiro
COPY SignalRDemo.slnx ./
COPY src/SignalRDemo.Domain/SignalRDemo.Domain.csproj src/SignalRDemo.Domain/
COPY src/SignalRDemo.Infrastructure/SignalRDemo.Infrastructure.csproj src/SignalRDemo.Infrastructure/
COPY src/SignalRDemo.Api/SignalRDemo.Api.csproj src/SignalRDemo.Api/
RUN dotnet restore src/SignalRDemo.Api/SignalRDemo.Api.csproj

# Copia o restante e publica
COPY . .
RUN dotnet publish src/SignalRDemo.Api/SignalRDemo.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SignalRDemo.Api.dll"]
