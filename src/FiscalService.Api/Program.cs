using FiscalService.Api.Config;
using FiscalService.Api.Configuration;
using FiscalService.Api.Data;
using FiscalService.Api.Middlewares;
using FiscalService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

// ── Bootstrap logger (antes do builder) ────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("FiscalService iniciando...");

try
{
    // .env na raiz do repositório (ou acima do diretório de trabalho) + aliases API_KEY/DB_PASSWORD → config ASP.NET
    EnvBootstrap.Apply();

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog (configuração via appsettings.json + env vars) ───────────────
    builder.Host.UseSerilog((ctx, services, configuration) =>
        configuration.ReadFrom.Configuration(ctx.Configuration)
                     .ReadFrom.Services(services)
                     .Enrich.FromLogContext());

    // ── Configurações tipadas ────────────────────────────────────────────────
    var fiscalConfig = builder.Configuration
        .GetSection(FiscalConfig.SectionName)
        .Get<FiscalConfig>() ?? new FiscalConfig();

    // Resolve caminhos relativos para o diretório de trabalho quando rodando localmente
    if (!Path.IsPathRooted(fiscalConfig.DiretorioXmls))
        fiscalConfig.DiretorioXmls = Path.Combine(Directory.GetCurrentDirectory(), fiscalConfig.DiretorioXmls);
    if (!Path.IsPathRooted(fiscalConfig.DiretorioCertificados))
        fiscalConfig.DiretorioCertificados = Path.Combine(Directory.GetCurrentDirectory(), fiscalConfig.DiretorioCertificados);
    if (!Path.IsPathRooted(fiscalConfig.DiretorioSchemas))
        fiscalConfig.DiretorioSchemas = Path.Combine(Directory.GetCurrentDirectory(), "Schemas");

    builder.Services.AddSingleton(fiscalConfig);

    // ── PostgreSQL / EF Core ─────────────────────────────────────────────────
    var connectionString = builder.Configuration["Database:ConnectionString"]
                        ?? builder.Configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException("Connection string não configurada.");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // ── Health Checks ────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgresql", tags: new[] { "db", "sql" });

    // ── Serviços fiscais (Transient — DFe.NET não é thread-safe) ────────────
    builder.Services.AddTransient<NFeService>();
    builder.Services.AddTransient<NFCeService>();
    builder.Services.AddTransient<CTeService>();
    builder.Services.AddTransient<MDFeService>();
    builder.Services.AddTransient<DanfeService>();
    builder.Services.AddTransient<NumeracaoService>();
    builder.Services.AddTransient<CertificadoService>();

    // ── Controllers + JSON ───────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "FiscalService API", Version = "v1" });
        c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Name = "X-Api-Key",
            Description = "API Key para autenticação"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // ── Build ────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Migrations automáticas no startup ────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            Log.Information("Migrations aplicadas com sucesso.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha ao aplicar migrations — banco pode já estar atualizado.");
        }
    }

    // ── Garantir que os diretórios de trabalho existam ───────────────────────
    Directory.CreateDirectory(fiscalConfig.DiretorioXmls);
    Directory.CreateDirectory(fiscalConfig.DiretorioCertificados);

    // ── Pipeline ─────────────────────────────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "FiscalService API v1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (ctx, httpCtx) =>
        {
            ctx.Set("RemoteIP", httpCtx.Connection.RemoteIpAddress?.ToString());
            ctx.Set("RequestHost", httpCtx.Request.Host.Value);
        };
    });

    // Middleware de autenticação por API Key (antes dos controllers)
    app.UseMiddleware<ApiKeyMiddleware>();

    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();

    // Health check endpoint (sem autenticação — liberado no middleware)
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString().ToLowerInvariant(),
                versao = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                timestamp = DateTime.UtcNow,
                banco = report.Entries.TryGetValue("postgresql", out var pg)
                    ? pg.Status.ToString().ToLowerInvariant()
                    : "desconhecido",
                schemas = Directory.Exists(fiscalConfig.DiretorioSchemas) ? "ok" : "diretorio_nao_encontrado"
            });
            await context.Response.WriteAsync(result);
        }
    });

    Log.Information("FiscalService iniciado. Escutando em {Urls}", string.Join(", ", app.Urls));
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "FiscalService encerrado inesperadamente.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
