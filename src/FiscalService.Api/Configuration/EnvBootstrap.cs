namespace FiscalService.Api.Configuration;

/// <summary>
/// Carrega o arquivo .env antes do <see cref="WebApplication.CreateBuilder(string[])"/>
/// para que variáveis entrem na configuração padrão (env + appsettings).
/// Compatibiliza nomes usados no <c>docker-compose.yml</c> (<c>API_KEY</c>, <c>DB_PASSWORD</c>, etc.)
/// com as chaves que o ASP.NET Core espera (<c>ApiKey</c>, <c>Database__ConnectionString</c>, …).
/// </summary>
public static class EnvBootstrap
{
    public static void Apply()
    {
        var dotEnvPath = FindDotEnvFilePath();
        if (dotEnvPath is not null)
            DotNetEnv.Env.Load(dotEnvPath);

        ApplyDockerStyleEnvAliases();
    }

    private static string? FindDotEnvFilePath()
    {
        var fromCurrent = SearchUpwardsForDotEnv(Directory.GetCurrentDirectory());
        if (fromCurrent is not null)
            return fromCurrent;

        return SearchUpwardsForDotEnv(AppContext.BaseDirectory);
    }

    private static string? SearchUpwardsForDotEnv(string startPath)
    {
        try
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startPath));
            for (var i = 0; i < 14 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, ".env");
                if (File.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }
        }
        catch
        {
            // caminho inválido em alguns hosts — ignora
        }

        return null;
    }

    private static void ApplyDockerStyleEnvAliases()
    {
        // ApiKey ← API_KEY (compose: ApiKey=${API_KEY})
        var apiKey = Environment.GetEnvironmentVariable("ApiKey");
        var apiKeyFile = Environment.GetEnvironmentVariable("API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiKeyFile))
            Environment.SetEnvironmentVariable("ApiKey", apiKeyFile);

        // Fiscal:Ambiente ← FISCAL_AMBIENTE
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Fiscal__Ambiente")))
        {
            var amb = Environment.GetEnvironmentVariable("FISCAL_AMBIENTE");
            if (!string.IsNullOrWhiteSpace(amb))
                Environment.SetEnvironmentVariable("Fiscal__Ambiente", amb);
        }

        // Fiscal:TimeoutWs ← FISCAL_TIMEOUT_WS
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Fiscal__TimeoutWs")))
        {
            var tw = Environment.GetEnvironmentVariable("FISCAL_TIMEOUT_WS");
            if (!string.IsNullOrWhiteSpace(tw))
                Environment.SetEnvironmentVariable("Fiscal__TimeoutWs", tw);
        }

        // Connection string completa OU montagem local a partir de DB_PASSWORD (+ opcionais)
        var conn = Environment.GetEnvironmentVariable("Database__ConnectionString");
        if (!string.IsNullOrWhiteSpace(conn))
            return;

        var pwd = Environment.GetEnvironmentVariable("DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(pwd))
            return;

        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "fiscal_db";
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? "fiscal_user";

        Environment.SetEnvironmentVariable(
            "Database__ConnectionString",
            $"Host={host};Port={port};Database={database};Username={user};Password={pwd}");
    }
}
