# Stage 1 — restore & build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/FinTrack.Domain/FinTrack.Domain.csproj", "FinTrack.Domain/"]
COPY ["src/FinTrack.Application/FinTrack.Application.csproj", "FinTrack.Application/"]
COPY ["src/FinTrack.Infrastructure/FinTrack.Infrastructure.csproj", "FinTrack.Infrastructure/"]
COPY ["src/FinTrack.Api/FinTrack.Api.csproj", "FinTrack.Api/"]
RUN dotnet restore "FinTrack.Api/FinTrack.Api.csproj"
COPY src/ ./src/
RUN dotnet publish "FinTrack.Api/FinTrack.Api.csproj" -c Release -o /app/publish --no-restore

# Stage 2 — runtime (Linux App Service / container apps ready)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FinTrack.Api.dll"]
