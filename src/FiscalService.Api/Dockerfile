# ── FiscalService.Api — imagem de produção ───────────────────────────────────
# Build context: raiz do repositório (onde existem as pastas src/, docker-compose.yml).
# No Easypanel: aponte o Dockerfile para src/FiscalService.Api/Dockerfile e o contexto
# de build para a raiz do repo (não use só a pasta FiscalService.Api — os COPY falham).
#
# Variáveis obrigatórias no painel (ou stack): ApiKey, Database__ConnectionString.
# Opcionais: FISCAL__* (ver README / docker-compose.yml).

# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FiscalService.Api/FiscalService.Api.csproj", "src/FiscalService.Api/"]
RUN dotnet restore "src/FiscalService.Api/FiscalService.Api.csproj"

COPY . .
WORKDIR "/src/src/FiscalService.Api"
RUN dotnet build "FiscalService.Api.csproj" -c Release -o /app/build

# ── Stage 2: Publish ─────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "FiscalService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 3: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# curl = healthcheck; fontes/libs = compatibilidade com libs gráficas/PDF eventualmente usadas pelo stack DFe
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libfreetype6 \
    libx11-6 \
    libxext6 \
    libxrender1 \
    fonts-dejavu-core \
    fonts-liberation \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Criar diretórios de dados (sobrescritos por volumes em produção)
RUN mkdir -p /app/xmls /app/schemas /app/certificados /app/logs

COPY --from=publish /app/publish .

# Copiar schemas do DFe.NET para o diretório padrão
COPY src/FiscalService.Api/Schemas /app/schemas

# Alinha com docker-compose e com o HEALTHCHECK (Easypanel não injeta isso sozinho)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FiscalService.Api.dll"]
