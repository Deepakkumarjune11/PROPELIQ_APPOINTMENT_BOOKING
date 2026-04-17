# ── Stage 1: build ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore using solution file — copies only project manifests first for layer caching
COPY PropelIQ.slnx ./
COPY global.json ./
COPY src/ ./src/
RUN dotnet restore PropelIQ.slnx

# Publish API in Release configuration
RUN dotnet publish src/PropelIQ.Api/PropelIQ.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# ── Stage 2: runtime ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security (OWASP A05 — Security Misconfiguration)
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

# Serilog file sink logs land here; mounted as named volume in compose
RUN mkdir -p /app/logs

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "PropelIQ.Api.dll"]
