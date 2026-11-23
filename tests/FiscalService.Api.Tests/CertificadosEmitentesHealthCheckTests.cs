using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.HealthChecks;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace FiscalService.Api.Tests;

public class CertificadosEmitentesHealthCheckTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppDbContext _db;
    private readonly CertificadoSenhaProtector _protector;
    private readonly FiscalConfig _config;

    public CertificadosEmitentesHealthCheckTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FiscalHealthTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var dpProvider = new EphemeralDataProtectionProvider();
        _protector = new CertificadoSenhaProtector(dpProvider);

        _config = new FiscalConfig
        {
            DiretorioCertificados = _tempDir,
            DiasAlertaCertificado = 30
        };
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private string CriarCertificadoPfx(string nomeArquivo, string senha, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Teste", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(notBefore, notAfter);
        var pfxBytes = cert.Export(X509ContentType.Pfx, senha);
        var fullPath = Path.Combine(_tempDir, nomeArquivo);
        File.WriteAllBytes(fullPath, pfxBytes);
        return fullPath;
    }

    [Fact]
    public async Task SemEmitentes_RetornaHealthyComMensagemAmigavel()
    {
        var check = new CertificadosEmitentesHealthCheck(_db, _config, _protector);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("Nenhum emitente ativo", result.Description);
        Assert.Equal(0, (int)result.Data["totalEmitentesAtivos"]);
    }

    [Fact]
    public async Task ArquivoNaoEncontrado_RetornaDegradedComAvisoClaro()
    {
        _db.Emitentes.Add(new Emitente
        {
            Cnpj = "11111111000199",
            RazaoSocial = "Empresa Teste",
            Uf = "SP",
            Ambiente = "Homologacao",
            Ativo = true,
            CertificadoPath = "inexistente.pfx",
            CertificadoSenhaProtegida = _protector.Proteger("123456")
        });
        await _db.SaveChangesAsync();

        var check = new CertificadosEmitentesHealthCheck(_db, _config, _protector);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Aviso:", result.Description);
        Assert.Contains("11111111000199", result.Description);
        Assert.True(result.Data.ContainsKey("detalhesAlertas"));
    }

    [Fact]
    public async Task CertificadoExpirado_RetornaDegradedComMensagemEmPortuguesECnpj()
    {
        var pfxPath = CriarCertificadoPfx(
            "expirado.pfx",
            "senha123",
            DateTimeOffset.UtcNow.AddDays(-60),
            DateTimeOffset.UtcNow.AddDays(-5));

        _db.Emitentes.Add(new Emitente
        {
            Cnpj = "22222222000188",
            RazaoSocial = "Empresa Expirada",
            Uf = "SP",
            Ambiente = "Homologacao",
            Ativo = true,
            CertificadoPath = pfxPath,
            CertificadoSenhaProtegida = _protector.Proteger("senha123")
        });
        await _db.SaveChangesAsync();

        var check = new CertificadosEmitentesHealthCheck(_db, _config, _protector);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Atenção:", result.Description);
        Assert.Contains("22222222000188", result.Description);
        Assert.Contains("webservice permanece ativo e operacional", result.Description);
        Assert.Contains("requer renovação do certificado", result.Description);
        Assert.Equal(1, (int)result.Data["certificadosExpirados"]);
        Assert.True(result.Data.ContainsKey("orientacao"));
    }

    [Fact]
    public async Task CertificadoValido_RetornaHealthyComMensagemEmPortugues()
    {
        var pfxPath = CriarCertificadoPfx(
            "valido.pfx",
            "senha123",
            DateTimeOffset.UtcNow.AddDays(-10),
            DateTimeOffset.UtcNow.AddDays(180));

        _db.Emitentes.Add(new Emitente
        {
            Cnpj = "33333333000177",
            RazaoSocial = "Empresa Valida",
            Uf = "SP",
            Ambiente = "Homologacao",
            Ativo = true,
            CertificadoPath = pfxPath,
            CertificadoSenhaProtegida = _protector.Proteger("senha123")
        });
        await _db.SaveChangesAsync();

        var check = new CertificadosEmitentesHealthCheck(_db, _config, _protector);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("certificado digital A1 verificado e válido", result.Description);
        Assert.Equal(1, (int)result.Data["certificadosValidos"]);
        Assert.Equal(0, (int)result.Data["certificadosExpirados"]);
    }
}
