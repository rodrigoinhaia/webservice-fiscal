using Npgsql;

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

        MergeApiKeyPreviousForRotation();

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

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Database__ConnectionString")))
            return;

        if (TryBuildConnectionStringFromPostgresUri())
            return;

        BuildConnectionStringFromPasswordParts();
    }

    /// <summary>
    /// Rotação sem downtime: <c>API_KEY_PREVIOUS</c> é aceita em conjunto com <c>API_KEY</c> / <c>ApiKey</c>
    /// (vira <c>ApiKey=nova,antiga</c> na configuração efetiva).
    /// </summary>
    private static void MergeApiKeyPreviousForRotation()
    {
        var previous = Environment.GetEnvironmentVariable("API_KEY_PREVIOUS")
                       ?? Environment.GetEnvironmentVariable("ApiKey__Previous");
        if (string.IsNullOrWhiteSpace(previous))
            return;

        var current = Environment.GetEnvironmentVariable("ApiKey");
        if (string.IsNullOrWhiteSpace(current))
            return;

        var prevTrim = previous.Trim();
        if (ApiKeyRing.Matches(current, prevTrim))
            return;

        Environment.SetEnvironmentVariable("ApiKey", $"{current.Trim().TrimEnd(',', '|', ';')},{prevTrim}");
    }

    /// <summary>
    /// Aceita <c>DATABASE_URL</c> ou <c>DB_PASSWORD</c> no formato <c>postgres://user:pass@host:port/db?sslmode=disable</c>
    /// (provedores costumam enviar URL; o Npgsql usa connection string com parâmetros).
    /// </summary>
    private static bool TryBuildConnectionStringFromPostgresUri()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            var dbPwd = Environment.GetEnvironmentVariable("DB_PASSWORD");
            if (LooksLikePostgresConnectionUri(dbPwd))
                url = dbPwd!;
        }

        if (string.IsNullOrWhiteSpace(url) || !LooksLikePostgresConnectionUri(url))
            return false;

        var built = BuildNpgsqlConnectionStringFromUri(url);
        if (string.IsNullOrWhiteSpace(built))
            return false;

        Environment.SetEnvironmentVariable("Database__ConnectionString", built);
        return true;
    }

    private static bool LooksLikePostgresConnectionUri(string? s) =>
        !string.IsNullOrWhiteSpace(s)
        && (s.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase));

    private static string? BuildNpgsqlConnectionStringFromUri(string connectionUri)
    {
        try
        {
            var uri = new Uri(connectionUri);
            var userParts = uri.UserInfo.Split(':', 2);
            var user = Uri.UnescapeDataString(userParts[0]);
            var password = userParts.Length > 1 ? Uri.UnescapeDataString(userParts[1]) : string.Empty;

            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;

            var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            if (string.IsNullOrEmpty(database))
                database = "postgres";

            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = user,
                Password = password
            };

            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length != 2) continue;
                    var key = kv[0].Trim();
                    var value = Uri.UnescapeDataString(kv[1].Trim());
                    if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                        csb.SslMode = MapSslMode(value);
                }
            }

            return csb.ConnectionString;
        }
        catch
        {
            return null;
        }
    }

    private static SslMode MapSslMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "disable" => SslMode.Disable,
            "allow" => SslMode.Allow,
            "prefer" => SslMode.Prefer,
            "require" => SslMode.Require,
            "verify-ca" => SslMode.VerifyCA,
            "verify-full" => SslMode.VerifyFull,
            _ => SslMode.Prefer
        };

    private static void BuildConnectionStringFromPasswordParts()
    {
        var pwd = Environment.GetEnvironmentVariable("DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(pwd) || LooksLikePostgresConnectionUri(pwd))
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
