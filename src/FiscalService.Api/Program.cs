using FiscalService.Api.Config;
using FiscalService.Api.Configuration;
using FiscalService.Api.Data;
using FiscalService.Api.Middlewares;
using FiscalService.Api.Services;
using FiscalService.Api.Services.Ibpt;
using FiscalService.Api.Services.NFSe;
using FiscalService.Api.HealthChecks;
using FiscalService.Api.Swagger;
using FiscalService.Api.Telemetry;
using FiscalService.Api.Validation;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// ── Bootstrap logger (antes do builder) ────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("FiscalService iniciando...");

try
{
    string ResolveDiretorioSchemas(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return Path.GetFullPath(configured);

        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Schemas"),
            Path.Combine(cwd, "Schemas"),
            Path.Combine(cwd, "src", "FiscalService.Api", "Schemas"),
        };

        foreach (var d in candidates)
        {
            try
            {
                var full = Path.GetFullPath(d);
                if (Directory.Exists(full))
                {
                    Log.Information("Fiscal:DiretorioSchemas resolvido para {Path} (fallback; config era {Config})", full, configured);
                    return full;
                }
            }
            catch
            {
                // ignora path inválido
            }
        }

        Log.Warning("Fiscal:DiretorioSchemas {Config} inexistente e nenhum fallback com pasta Schemas encontrado.",
            configured ?? "(vazio)");

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Schemas"));
    }

    string ResolveDiretorioSchemasNfse(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return Path.GetFullPath(configured);

        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Schemas", "NFSe"),
            Path.Combine(cwd, "Schemas", "NFSe"),
            Path.Combine(cwd, "src", "FiscalService.Api", "Schemas", "NFSe"),
        };

        foreach (var d in candidates)
        {
            try
            {
                var full = Path.GetFullPath(d);
                if (Directory.Exists(full))
                {
                    Log.Information("Fiscal:NFSe:DiretorioSchemas resolvido para {Path}", full);
                    return full;
                }
            }
            catch
            {
                // ignora path inválido
            }
        }

        return Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "Schemas", "NFSe")
            : configured);
    }

    // .env na raiz do repositório
    EnvBootstrap.Apply();

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog (configuração via appsettings.json + env vars) ───────────────
    // Sink de arquivo é opcional: só se Serilog:File estiver habilitado e o diretório for gravável.
    builder.Host.UseSerilog((ctx, services, configuration) =>
    {
        configuration.ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();

        SerilogFileSinkHelper.TryAddFileSink(configuration, ctx.Configuration);
    });

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
        fiscalConfig.DiretorioSchemas = Path.Combine(Directory.GetCurrentDirectory(), fiscalConfig.DiretorioSchemas);
    fiscalConfig.DiretorioSchemas = ResolveDiretorioSchemas(fiscalConfig.DiretorioSchemas);

    if (!Path.IsPathRooted(fiscalConfig.NFSe.DiretorioSchemas))
        fiscalConfig.NFSe.DiretorioSchemas = Path.Combine(Directory.GetCurrentDirectory(), fiscalConfig.NFSe.DiretorioSchemas);
    fiscalConfig.NFSe.DiretorioSchemas = ResolveDiretorioSchemasNfse(fiscalConfig.NFSe.DiretorioSchemas);

    builder.Services.AddSingleton(fiscalConfig);
    builder.Services.Configure<FormOptions>(o =>
    {
        o.MultipartBodyLengthLimit = 30 * 1024 * 1024;
        o.ValueLengthLimit = 30 * 1024 * 1024;
    });
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 30 * 1024 * 1024);
    Log.Information(
        "Fiscal SEFAZ: Ambiente={Ambiente} TimeoutWs={TimeoutWs}s ({TimeoutMs}ms no DFe.NET)",
        fiscalConfig.Ambiente,
        fiscalConfig.TimeoutWs,
        fiscalConfig.TimeoutWsMilliseconds);

    var rateLimiting = builder.Configuration.GetSection(RateLimitingConfig.SectionName).Get<RateLimitingConfig>()
                       ?? new RateLimitingConfig();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (ctx, cancellationToken) =>
        {
            ctx.HttpContext.Response.ContentType = "application/json";
            await ctx.HttpContext.Response.WriteAsJsonAsync(new
            {
                sucesso = false,
                erro = new
                {
                    tipo = "LimiteExcedido",
                    mensagem = "Muitas requisições. Tente novamente em instantes.",
                    timestamp = DateTime.UtcNow
                }
            }, cancellationToken);
        };

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            if (!rateLimiting.Enabled)
                return RateLimitPartition.GetNoLimiter("off");

            if (httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                return RateLimitPartition.GetNoLimiter("health");

            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                ip,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = Math.Max(1, rateLimiting.PermitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, rateLimiting.WindowSeconds)),
                    QueueLimit = 0
                });
        });
    });

    // ── PostgreSQL / EF Core ─────────────────────────────────────────────────
    static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    var connectionString = FirstNonEmpty(
            builder.Configuration["Database:ConnectionString"],
            builder.Configuration.GetConnectionString("DefaultConnection"))
        ?? throw new InvalidOperationException(
            "Connection string não configurada. Defina Database__ConnectionString ou DATABASE_URL/DB_PASSWORD no .env.");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Data Protection: padrão PostgreSQL (sobrevive a redeploy sem volume /app/keys).
    // Provider=FileSystem mantém compatibilidade com instalações antigas.
    var dataProtectionProvider = builder.Configuration["DataProtection:Provider"] ?? "Database";
    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName("FiscalService.Api");
    if (dataProtectionProvider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
    {
        var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"] ?? "/app/keys";
        Directory.CreateDirectory(dataProtectionKeysPath);
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        Log.Information("Data Protection: key ring em disco ({Path})", dataProtectionKeysPath);
    }
    else
    {
        dataProtection.PersistKeysToDbContext<AppDbContext>();
        Log.Information("Data Protection: key ring no PostgreSQL (tabela data_protection_keys)");
    }
    builder.Services.AddScoped<CertificadoSenhaProtector>();
    builder.Services.AddScoped<EmitenteService>();

    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IbptCacheStamp>();
    builder.Services.AddHttpClient<IbptApiClient>((sp, client) =>
    {
        var timeout = sp.GetRequiredService<FiscalConfig>().Ibpt.TimeoutSegundos;
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeout, 2, 60));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "FiscalService/IBPT");
    });
    builder.Services.AddSingleton<IbptTabelaArquivoStore>();
    builder.Services.AddScoped<IIbptAliquotaLookup, IbptAliquotaLookup>();
    builder.Services.AddScoped<IbptTributoService>();
    Log.Information(
        "IBPT: Habilitado={Habilitado} TokenGlobal={Token} Tabela={Tabela} InfCpl={InfCpl}",
        fiscalConfig.Ibpt.Habilitado,
        !string.IsNullOrWhiteSpace(fiscalConfig.Ibpt.Token),
        string.IsNullOrWhiteSpace(fiscalConfig.Ibpt.ArquivoTabela) ? "(nao configurada)" : fiscalConfig.Ibpt.ArquivoTabela,
        fiscalConfig.Ibpt.IncluirInfCpl);

    // ── Health Checks ────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgresql", tags: new[] { "db", "sql" })
        .AddCheck<CertificadosEmitentesHealthCheck>("certificados_emitentes", tags: new[] { "certificado", "emitente" });

    // ── Serviços fiscais (Transient — DFe.NET não é thread-safe) ────────────
    builder.Services.AddTransient<NFeService>();
    builder.Services.AddTransient<NFeDfeService>();
    builder.Services.AddTransient<NFCeService>();
    builder.Services.AddTransient<CTeService>();
    builder.Services.AddTransient<MDFeService>();
    builder.Services.AddTransient<DanfeService>();
    builder.Services.AddTransient<NumeracaoService>();
    builder.Services.AddTransient<CertificadoService>();
    builder.Services.AddTransient<NFSeOpenAcFactory>();
    builder.Services.AddTransient<NFSeService>();
    builder.Services.AddScoped<EmissaoLogService>();

    var otelCfg = builder.Configuration.GetSection(OpenTelemetryConfig.SectionName).Get<OpenTelemetryConfig>() ?? new();
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                       ?? builder.Configuration["OpenTelemetry:OtlpEndpoint"]
                       ?? otelCfg.OtlpEndpoint;
    var otelEnv = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
    var otelOn = otelCfg.Enabled || otelEnv;
    if (otelOn && !string.IsNullOrWhiteSpace(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri))
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService("FiscalService"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = otlpUri))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(FiscalTelemetry.MeterName)
                .AddOtlpExporter(o => o.Endpoint = otlpUri));
    }
    else if (otelOn && string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        Log.Warning("OpenTelemetry ligado (config ou env) mas OTLP endpoint ausente — exportação ignorada.");
    }

    // ── Controllers + JSON ───────────────────────────────────────────────────
    builder.Services.AddControllers(options => { options.Filters.Add<FiscalResponseTelemetryFilter>(); })
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();
    builder.Services.AddValidatorsFromAssemblyContaining<ConfiguracaoEmitenteRequestValidator>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "FiscalService API", Version = "v1" });
        c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Name = "X-Api-Key",
            Description =
                "Chave de API. Aceita uma chave ou várias separadas por vírgula/ponto-e-vírgula em configuração (rotação). " +
                "Em Docker/.env: defina API_KEY; opcionalmente API_KEY_PREVIOUS durante rotação (ambas válidas)."
        });
        c.OperationFilter<OpenApiCommonResponsesOperationFilter>();
        c.OperationFilter<OpenApiJsonExamplesFilter>();
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
    Directory.CreateDirectory(fiscalConfig.Ibpt.ResolverDiretorio());

    // ── Pipeline ─────────────────────────────────────────────────────────────
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FiscalService API v1");
        c.RoutePrefix = "swagger";
    });

    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (ctx, httpCtx) =>
        {
            ctx.Set("RemoteIP", httpCtx.Connection.RemoteIpAddress?.ToString());
            ctx.Set("RequestHost", httpCtx.Request.Host.Value);
        };
    });

    app.UseRouting();

    app.UseRateLimiter();

    // Middleware de autenticação por API Key (após rate limit)
    app.UseMiddleware<ApiKeyMiddleware>();

    app.UseAuthorization();
    app.MapControllers();

    // Health check endpoint (sem autenticação — liberado no middleware)
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var certEntry = report.Entries.TryGetValue("certificados_emitentes", out var cert)
                ? cert.Status.ToString().ToLowerInvariant()
                : "nao_configurado";

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString().ToLowerInvariant(),
                versao = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                timestamp = DateTime.UtcNow,
                banco = report.Entries.TryGetValue("postgresql", out var pg)
                    ? pg.Status.ToString().ToLowerInvariant()
                    : "desconhecido",
                certificados = certEntry,
                schemas = Directory.Exists(fiscalConfig.DiretorioSchemas) ? "ok" : "diretorio_nao_encontrado",
                schemasNfse = !fiscalConfig.NFSe.Habilitado
                    ? "modulo_desabilitado"
                    : Directory.Exists(fiscalConfig.NFSe.DiretorioSchemas) ? "ok" : "diretorio_nao_encontrado",
                checks = report.Entries.ToDictionary(
                    e => e.Key,
                    e => e.Value.Status.ToString().ToLowerInvariant())
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
