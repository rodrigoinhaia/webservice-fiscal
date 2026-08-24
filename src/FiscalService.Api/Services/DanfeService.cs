using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services.Danfe;
using FiscalService.Api.Services.DanfeHtml;

namespace FiscalService.Api.Services;

/// <summary>
/// Geração de DANFE em PDF. NF-e via <see cref="NFeDanfeLocalRenderer"/> (Danfe.NFe.Core / Linux).
/// NFC-e PDF permanece pendente de biblioteca cross-platform.
/// </summary>
public class DanfeService
{
    private readonly ILogger<DanfeService> _logger;
    private readonly NFeDanfeLocalRenderer _nfeDanfeLocal;

    public DanfeService(ILogger<DanfeService> logger, NFeDanfeLocalRenderer nfeDanfeLocal)
    {
        _logger = logger;
        _nfeDanfeLocal = nfeDanfeLocal;
    }

    /// <summary>Gera o DANFE NF-e a partir do XML do nfeProc e retorna em base64.</summary>
    public string GerarNFePdf(string xmlNfeProc)
    {
        var pdf = _nfeDanfeLocal.TentarGerarDeXml(xmlNfeProc);
        if (pdf is null or { Length: 0 })
        {
            _logger.LogWarning("DANFE NF-e não gerado a partir do XML informado.");
            throw new InvalidOperationException(
                "Não foi possível gerar o PDF do DANFE. Informe um XML nfeProc válido (NFe + protNFe).");
        }

        return Convert.ToBase64String(pdf);
    }

    /// <summary>Gera o DANFE NFC-e a partir do XML do nfeProc e retorna em base64.</summary>
    public string GerarNFCePdf(string xmlNfeProc, string idCsc, string csc)
    {
        _ = idCsc;
        _ = csc;
        _logger.LogWarning("Geração de DANFE NFC-e em PDF ainda não está disponível neste ambiente.");
        throw new NotSupportedException(
            "Geração de DANFE NFC-e em PDF não está disponível. Use o endpoint HTML ou aguarde integração QuestPDF.");
    }

    public DanfeResponse GerarNFePdfResponse(string xmlNfeProc)
    {
        try
        {
            return new DanfeResponse
            {
                Sucesso = true,
                PdfBase64 = GerarNFePdf(xmlNfeProc)
            };
        }
        catch (Exception ex)
        {
            return new DanfeResponse
            {
                Sucesso = false,
                Erro = new ErroResponse
                {
                    Tipo = ex is NotSupportedException ? "NaoSuportado" : "ErroInterno",
                    Mensagem = "Erro ao gerar PDF do DANFE.",
                    Detalhe = ex.Message,
                    Timestamp = DateTime.UtcNow
                }
            };
        }
    }

    public DanfeResponse GerarNFCePdfResponse(string xmlNfeProc, string idCsc, string csc)
    {
        try
        {
            return new DanfeResponse
            {
                Sucesso = true,
                PdfBase64 = GerarNFCePdf(xmlNfeProc, idCsc, csc)
            };
        }
        catch (Exception ex)
        {
            return new DanfeResponse
            {
                Sucesso = false,
                Erro = new ErroResponse
                {
                    Tipo = ex is NotSupportedException ? "NaoSuportado" : "ErroInterno",
                    Mensagem = "Erro ao gerar PDF do DANFE NFC-e.",
                    Detalhe = ex.Message,
                    Timestamp = DateTime.UtcNow
                }
            };
        }
    }

    /// <summary>DANFE NF-e em HTML (impressão / PDF pelo navegador).</summary>
    public DanfeHtmlResponse GerarNFeHtmlResponse(string xmlNfeProc)
    {
        try
        {
            var html = DanfeHtmlRenderer.GerarNFe(xmlNfeProc);
            return new DanfeHtmlResponse { Sucesso = true, Html = html };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar DANFE NF-e em HTML.");
            return new DanfeHtmlResponse
            {
                Sucesso = false,
                Erro = new ErroResponse
                {
                    Tipo = "ErroInterno",
                    Mensagem = "Não foi possível gerar o HTML do DANFE a partir do XML informado.",
                    Detalhe = ex.Message,
                    Timestamp = DateTime.UtcNow
                }
            };
        }
    }

    /// <summary>DANFE NFC-e em HTML (impressão / PDF pelo navegador).</summary>
    public DanfeHtmlResponse GerarNFCeHtmlResponse(string xmlNfeProc, string idCsc, string csc)
    {
        _ = idCsc;
        _ = csc;
        try
        {
            var html = DanfeHtmlRenderer.GerarNFCe(xmlNfeProc);
            return new DanfeHtmlResponse { Sucesso = true, Html = html };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar DANFE NFC-e em HTML.");
            return new DanfeHtmlResponse
            {
                Sucesso = false,
                Erro = new ErroResponse
                {
                    Tipo = "ErroInterno",
                    Mensagem = "Não foi possível gerar o HTML a partir do XML informado.",
                    Detalhe = ex.Message,
                    Timestamp = DateTime.UtcNow
                }
            };
        }
    }
}
