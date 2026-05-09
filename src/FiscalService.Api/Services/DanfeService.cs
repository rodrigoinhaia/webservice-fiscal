using FiscalService.Api.Models.Responses;

namespace FiscalService.Api.Services;

/// <summary>
/// Geração de DANFE em PDF. Implementação atual não gera PDF em Linux; ver <c>docs/DANFE-ESTRATEGIA.md</c>.
/// A dependência NFe.Danfe.Nativo é Windows-only e não pode ser usada neste ambiente.
/// Para produção, utilize uma solução cross-platform (ex: DanfeSharp, QuestPDF, etc.).
/// </summary>
public class DanfeService
{
    private readonly ILogger<DanfeService> _logger;

    public DanfeService(ILogger<DanfeService> logger)
    {
        _logger = logger;
    }

    /// <summary>Gera o DANFE NF-e a partir do XML do nfeProc e retorna em base64.</summary>
    public string GerarNFePdf(string xmlNfeProc)
    {
        _logger.LogWarning("Geração de DANFE NF-e não está disponível neste ambiente (requer biblioteca cross-platform).");
        throw new NotSupportedException(
            "Geração de DANFE não está disponível. Instale uma biblioteca de renderização compatível com Linux (ex: DanfeSharp).");
    }

    /// <summary>Gera o DANFE NFC-e a partir do XML do nfeProc e retorna em base64.</summary>
    public string GerarNFCePdf(string xmlNfeProc, string idCsc, string csc)
    {
        _logger.LogWarning("Geração de DANFE NFC-e não está disponível neste ambiente.");
        throw new NotSupportedException(
            "Geração de DANFE NFC-e não está disponível. Instale uma biblioteca de renderização compatível com Linux.");
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
}
