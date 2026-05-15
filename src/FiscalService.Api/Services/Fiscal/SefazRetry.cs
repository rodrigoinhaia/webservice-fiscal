using FiscalService.Api.Config;

namespace FiscalService.Api.Services.Fiscal;

/// <summary>
/// Retentativas apenas para falhas transitórias de rede/timeout.
/// Rejeições de negócio da SEFAZ (cStat) não passam por aqui — o serviço retorna normalmente.
/// </summary>
public static class SefazRetry
{
    public static T Execute<T>(FiscalConfig config, ILogger logger, string operacao, Func<T> action)
    {
        if (!config.SefazRetryHabilitado || config.SefazRetryMaxTentativas <= 1)
            return action();

        var max = config.SefazRetryMaxTentativas;
        var baseDelay = Math.Max(100, config.SefazRetryIntervaloMs);
        Exception? last = null;

        for (var tentativa = 1; tentativa <= max; tentativa++)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (tentativa < max && DeveRetentar(ex))
            {
                last = ex;
                var espera = baseDelay * tentativa;
                logger.LogWarning(ex,
                    "SEFAZ {Operacao}: falha transitória (tentativa {Tentativa}/{Max}), aguardando {Delay}ms",
                    operacao, tentativa, max, espera);
                Thread.Sleep(espera);
            }
        }

        throw last ?? new InvalidOperationException($"SEFAZ {operacao}: falha sem exceção capturada.");
    }

    public static bool DeveRetentar(Exception ex)
    {
        if (ex is TributacaoNaoSuportadaException or ArgumentException or KeyNotFoundException)
            return false;

        if (ex is TimeoutException or System.Net.Http.HttpRequestException)
            return true;

        if (ex is System.Net.WebException)
            return true;

        var msg = ex.Message.ToLowerInvariant();
        if (ex.InnerException is not null)
            msg += " " + ex.InnerException.Message.ToLowerInvariant();

        return msg.Contains("timeout")
               || msg.Contains("timed out")
               || msg.Contains("connection")
               || msg.Contains("unavailable")
               || msg.Contains("could not establish")
               || msg.Contains("temporarily")
               || msg.Contains("host desconhecido")
               || msg.Contains("name resolution")
               || msg.Contains("ssl")
               && msg.Contains("handshake");
    }
}
