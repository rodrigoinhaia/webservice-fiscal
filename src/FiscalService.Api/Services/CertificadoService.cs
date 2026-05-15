using FiscalService.Api.Config;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using System.Security.Cryptography.X509Certificates;

namespace FiscalService.Api.Services;

public class CertificadoService
{
    private readonly FiscalConfig _config;
    private readonly ILogger<CertificadoService> _logger;

    public CertificadoService(FiscalConfig config, ILogger<CertificadoService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public CertificadoValidarResponse Validar(CertificadoValidarRequest request)
    {
        try
        {
            var bytes = Convert.FromBase64String(request.CertificadoBase64);
            using var cert = new X509Certificate2(bytes, request.Senha, X509KeyStorageFlags.EphemeralKeySet);

            var cnpj = ExtrairCnpjDoCertificado(cert);

            _logger.LogInformation("Certificado validado: CN={CN}, Validade={Validade}", cert.Subject, cert.NotAfter);

            return new CertificadoValidarResponse
            {
                Valido = true,
                Cnpj = cnpj,
                RazaoSocial = ExtrairRazaoSocial(cert),
                Validade = cert.NotAfter.ToUniversalTime(),
                Emissor = cert.Issuer,
                Thumbprint = cert.Thumbprint
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Falha na validação de certificado: {Mensagem}", ex.Message);
            return new CertificadoValidarResponse
            {
                Valido = false,
                Erro = new ErroResponse
                {
                    Tipo = "CertificadoInvalido",
                    Mensagem = "Certificado inválido, expirado ou senha incorreta.",
                    Detalhe = ex.Message,
                    Timestamp = DateTime.UtcNow
                }
            };
        }
    }

    public CertificadoUploadResponse Upload(CertificadoUploadRequest request)
    {
        try
        {
            var bytes = Convert.FromBase64String(request.ConteudoBase64);
            return SalvarCertificadoValidado(bytes, request.Nome, request.Senha);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer upload de certificado (JSON)");
            return RespostaUploadErro(ex);
        }
    }

    /// <summary>Recebe o conteúdo binário do .pfx/.p12 (ex.: multipart) e grava após validar com a senha.</summary>
    public CertificadoUploadResponse UploadArquivoBinario(byte[] bytes, string nomeArquivo, string senha)
    {
        try
        {
            return SalvarCertificadoValidado(bytes, nomeArquivo, senha);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer upload de certificado (arquivo)");
            return RespostaUploadErro(ex);
        }
    }

    private CertificadoUploadResponse SalvarCertificadoValidado(byte[] bytes, string nomeInformado, string senha)
    {
        ValidarPfx(bytes, senha);

        if (!Directory.Exists(_config.DiretorioCertificados))
            Directory.CreateDirectory(_config.DiretorioCertificados);

        var nomeSeguro = Path.GetFileName(nomeInformado);
        if (string.IsNullOrWhiteSpace(nomeSeguro))
            nomeSeguro = "certificado.pfx";
        if (!nomeSeguro.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
            && !nomeSeguro.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
            nomeSeguro += ".pfx";

        var pathAbsoluto = Path.Combine(_config.DiretorioCertificados, nomeSeguro);
        File.WriteAllBytes(pathAbsoluto, bytes);

        _logger.LogInformation("Certificado salvo: {Path}", pathAbsoluto);

        return new CertificadoUploadResponse
        {
            Sucesso = true,
            PathRelativo = nomeSeguro,
            PathAbsoluto = pathAbsoluto
        };
    }

    private static void ValidarPfx(byte[] bytes, string senha)
    {
        using var cert = new X509Certificate2(bytes, senha, X509KeyStorageFlags.EphemeralKeySet);
    }

    private static CertificadoUploadResponse RespostaUploadErro(Exception ex) => new()
    {
        Sucesso = false,
        Erro = new ErroResponse
        {
            Tipo = "CertificadoInvalido",
            Mensagem = "Falha ao processar ou salvar o certificado.",
            Detalhe = ex.Message,
            Timestamp = DateTime.UtcNow
        }
    };

    public string ResolvePath(string path) => _config.ResolveCertificadoPath(path);

    /// <summary>Carrega um X509Certificate2 a partir do path configurado, resolvendo path relativo se necessário.</summary>
    public X509Certificate2 CarregarCertificado(string path, string senha)
    {
        var pathAbsoluto = ResolvePath(path);

        if (!File.Exists(pathAbsoluto))
            throw new FileNotFoundException($"Certificado não encontrado: {pathAbsoluto}");

        return new X509Certificate2(pathAbsoluto, senha, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    private static string? ExtrairCnpjDoCertificado(X509Certificate2 cert)
    {
        // O CNPJ geralmente está no campo CN ou como OID no subject
        var subject = cert.Subject;
        var parts = subject.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            // Padrão ICP-Brasil: "CN=RAZAO SOCIAL:00000000000000"
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                var cn = trimmed[3..];
                var separatorIndex = cn.LastIndexOf(':');
                if (separatorIndex >= 0)
                {
                    var possibleCnpj = cn[(separatorIndex + 1)..].Trim();
                    if (possibleCnpj.Length == 14 && possibleCnpj.All(char.IsDigit))
                        return possibleCnpj;
                }
            }
        }

        return null;
    }

    private static string? ExtrairRazaoSocial(X509Certificate2 cert)
    {
        var subject = cert.Subject;
        var parts = subject.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                var cn = trimmed[3..];
                var separatorIndex = cn.LastIndexOf(':');
                return separatorIndex >= 0 ? cn[..separatorIndex].Trim() : cn.Trim();
            }
        }

        return null;
    }
}
