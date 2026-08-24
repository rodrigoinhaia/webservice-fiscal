using Danfe.NFe.Core;
using Danfe.NFe.Core.Modelo;
using NFe.Classes;

namespace FiscalService.Api.Services.Danfe;

/// <summary>
/// Gera DANFE NF-e (modelo 55) em PDF via <c>Danfe.NFe.Core</c> (PdfSharpCore, cross-platform).
/// </summary>
public sealed class NFeDanfeLocalRenderer
{
    private readonly ILogger<NFeDanfeLocalRenderer> _logger;

    public NFeDanfeLocalRenderer(ILogger<NFeDanfeLocalRenderer> logger)
    {
        _logger = logger;
    }

    public byte[]? TentarGerarDeXml(string? xmlNfeProc, byte[]? logoBytes = null)
    {
        if (string.IsNullOrWhiteSpace(xmlNfeProc))
            return null;

        try
        {
            var normalizado = NFeProcComposer.NormalizarParaDanfe(xmlNfeProc);
            return GerarDeXmlNormalizado(normalizado, logoBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar DANFE NF-e local a partir do XML.");
            return null;
        }
    }

    public byte[]? TentarGerarDeProc(nfeProc? proc, byte[]? logoBytes = null)
    {
        if (proc is null)
            return null;

        try
        {
            return GerarDeXmlNormalizado(proc.ObterXmlString(), logoBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar DANFE NF-e local a partir do nfeProc.");
            return null;
        }
    }

    private static byte[] GerarDeXmlNormalizado(string xml, byte[]? logoBytes)
    {
        var viewModel = DanfeViewModelCreator.CriarDeStringXml(xml);
        using var doc = new DanfeDoc(viewModel);
        if (logoBytes is { Length: > 0 })
        {
            using var logoStream = new MemoryStream(logoBytes);
            doc.AdicionarLogoImagem(logoStream);
        }

        doc.Gerar();
        using var pdfStream = new MemoryStream();
        doc.ObterPdfBytes(pdfStream);
        return pdfStream.ToArray();
    }
}
