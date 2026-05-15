using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Cryptography.X509Certificates;

namespace FiscalService.Api.HealthChecks;

/// <summary>Verifica validade dos certificados A1 dos emitentes cadastrados.</summary>
public sealed class CertificadosEmitentesHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    private readonly FiscalConfig _config;
    private readonly CertificadoSenhaProtector _senhaProtector;

    public CertificadosEmitentesHealthCheck(
        AppDbContext db,
        FiscalConfig config,
        CertificadoSenhaProtector senhaProtector)
    {
        _db = db;
        _config = config;
        _senhaProtector = senhaProtector;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var alertaDias = _config.DiasAlertaCertificado;
        var emitentes = await _db.Emitentes.AsNoTracking()
            .Where(e => e.Ativo)
            .ToListAsync(cancellationToken);

        if (emitentes.Count == 0)
        {
            return HealthCheckResult.Healthy("Nenhum emitente cadastrado para verificar certificado.",
                new Dictionary<string, object> { ["emitentes"] = 0 });
        }

        var expirados = new List<string>();
        var alerta = new List<string>();
        var ok = 0;

        foreach (var e in emitentes)
        {
            try
            {
                var path = _config.ResolveCertificadoPath(e.CertificadoPath);
                if (!File.Exists(path))
                {
                    alerta.Add($"{e.Cnpj}: arquivo não encontrado ({e.CertificadoPath})");
                    continue;
                }

                var senha = _senhaProtector.Desproteger(e.CertificadoSenhaProtegida);
                using var cert = new X509Certificate2(path, senha, X509KeyStorageFlags.EphemeralKeySet);
                var dias = (cert.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays;

                if (dias < 0)
                    expirados.Add($"{e.Cnpj}: expirado em {cert.NotAfter:yyyy-MM-dd}");
                else if (dias <= alertaDias)
                    alerta.Add($"{e.Cnpj}: expira em {cert.NotAfter:yyyy-MM-dd} ({(int)dias} dias)");
                else
                    ok++;
            }
            catch (Exception ex)
            {
                alerta.Add($"{e.Cnpj}: {ex.Message}");
            }
        }

        var data = new Dictionary<string, object>
        {
            ["emitentesAtivos"] = emitentes.Count,
            ["certificadosOk"] = ok,
            ["alertaDias"] = alertaDias
        };

        if (expirados.Count > 0)
        {
            data["expirados"] = expirados;
            return HealthCheckResult.Unhealthy("Certificado(s) expirado(s).", data: data);
        }

        if (alerta.Count > 0)
        {
            data["alertas"] = alerta;
            return HealthCheckResult.Degraded("Certificado(s) próximo(s) do vencimento ou com problema.", data: data);
        }

        return HealthCheckResult.Healthy("Certificados dos emitentes válidos.", data);
    }
}
