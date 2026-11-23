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
            return HealthCheckResult.Healthy(
                "Nenhum emitente ativo cadastrado no sistema.",
                new Dictionary<string, object>
                {
                    ["totalEmitentesAtivos"] = 0,
                    ["certificadosValidos"] = 0,
                    ["certificadosExpirados"] = 0,
                    ["certificadosEmAlerta"] = 0,
                    ["diasLimiteAlerta"] = alertaDias,
                    ["statusGeral"] = "SemEmitentes"
                });
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
                    alerta.Add($"{e.Cnpj}: arquivo do certificado não encontrado ({e.CertificadoPath})");
                    continue;
                }

                var senha = _senhaProtector.Desproteger(e.CertificadoSenhaProtegida);
                using var cert = new X509Certificate2(path, senha, X509KeyStorageFlags.EphemeralKeySet);
                var dias = (cert.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays;

                if (dias < 0)
                    expirados.Add($"{e.Cnpj}: expirado em {cert.NotAfter:yyyy-MM-dd}");
                else if (dias <= alertaDias)
                    alerta.Add($"{e.Cnpj}: expira em {cert.NotAfter:yyyy-MM-dd} ({(int)dias} dia(s) restante(s))");
                else
                    ok++;
            }
            catch (Exception ex)
            {
                alerta.Add($"{e.Cnpj}: erro ao ler certificado ({ex.Message})");
            }
        }

        var data = new Dictionary<string, object>
        {
            ["totalEmitentesAtivos"] = emitentes.Count,
            ["certificadosValidos"] = ok,
            ["certificadosExpirados"] = expirados.Count,
            ["certificadosEmAlerta"] = alerta.Count,
            ["diasLimiteAlerta"] = alertaDias,
            ["statusGeral"] = expirados.Count > 0 ? "Expirado" : (alerta.Count > 0 ? "Alerta" : "Regular")
        };

        if (expirados.Count > 0)
        {
            data["expirados"] = expirados;
            data["detalhesExpirados"] = expirados;
            data["orientacao"] = "Acesse o painel administrativo na aba Emitentes para atualizar o certificado digital A1 (.pfx).";

            var cnpjs = string.Join(", ", expirados.Select(x => x.Split(':')[0].Trim()));
            var mensagem = $"Atenção: {expirados.Count} certificado(s) digital(is) A1 expirado(s) [{cnpjs}]. O webservice permanece ativo e operacional, mas a emissão fiscal para estes emitentes requer renovação do certificado digital no painel.";
            if (alerta.Count > 0)
            {
                data["alertas"] = alerta;
                data["detalhesAlertas"] = alerta;
                mensagem += $" Além disso, {alerta.Count} certificado(s) requer(em) atenção ou estão próximos do vencimento.";
            }

            // Certificado expirado é Degraded (não Unhealthy) para não derrubar o container no health check do Docker/Easypanel,
            // permitindo que o serviço continue rodando e o usuário acesse o painel para fazer upload de novo certificado.
            return HealthCheckResult.Degraded(mensagem, data: data);
        }

        if (alerta.Count > 0)
        {
            data["alertas"] = alerta;
            data["detalhesAlertas"] = alerta;
            data["orientacao"] = "Verifique os certificados próximos do vencimento no painel web para evitar interrupções na emissão.";

            var cnpjs = string.Join(", ", alerta.Select(x => x.Split(':')[0].Trim()));
            var mensagem = $"Aviso: {alerta.Count} certificado(s) digital(is) A1 requer(em) atenção ou estão próximos do vencimento (limite de alerta: {alertaDias} dias) [{cnpjs}]. Providencie a renovação com antecedência no painel.";
            return HealthCheckResult.Degraded(mensagem, data: data);
        }

        var msgOk = ok == 1
            ? "1 certificado digital A1 verificado e válido."
            : $"{ok} certificados digitais A1 verificados e válidos.";
        return HealthCheckResult.Healthy(msgOk, data);
    }
}
