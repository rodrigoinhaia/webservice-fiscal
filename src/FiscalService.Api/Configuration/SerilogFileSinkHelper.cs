using Serilog;

namespace FiscalService.Api.Configuration;

/// <summary>
/// Adiciona o sink de arquivo só se o diretório existir e for gravável (evita falha em FS somente leitura ou sem /app/logs).
/// </summary>
public static class SerilogFileSinkHelper
{
    public static void TryAddFileSink(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        var section = configuration.GetSection("Serilog:File");
        if (!section.Exists())
            return;

        if (configuration.GetValue("Serilog:File:Disabled", false))
            return;

        var path = section["Path"];
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!TryPrepareLogDirectory(path, out var reason))
        {
            WriteStdErr(
                $"[FiscalService] Serilog: sink de arquivo desativado ({reason}). Logs apenas no console.");
            return;
        }

        var rolling = Enum.TryParse<RollingInterval>(section["RollingInterval"], ignoreCase: true, out var ri)
            ? ri
            : RollingInterval.Day;

        var retained = section.GetValue("RetainedFileCountLimit", 30);

        loggerConfiguration.WriteTo.File(
            path,
            rollingInterval: rolling,
            retainedFileCountLimit: retained);
    }

    private static bool TryPrepareLogDirectory(string filePath, out string reason)
    {
        reason = string.Empty;
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir))
            {
                reason = "caminho sem diretório";
                return false;
            }

            Directory.CreateDirectory(dir);

            var probe = Path.Combine(dir, $".serilog-write-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok", System.Text.Encoding.UTF8);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    private static void WriteStdErr(string message)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch
        {
            // ignore
        }
    }
}
