using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.DANFSe.PDFSharp;
using OpenAC.Net.NFSe.Nacional.DANFSe.PDFSharp.Configuracao;

namespace FiscalService.Api.Services.NFSe;

/// <summary>
/// Gera DANFSe em PDF a partir do XML autorizado (NT 008 / layout nacional),
/// via <c>OpenAC.Net.NFSe.Nacional.DANFSe.PDFSharp</c> — sem depender da API ADN de PDF.
/// </summary>
public sealed class NFSeDanfseLocalRenderer
{
    private readonly ILogger<NFSeDanfseLocalRenderer> _logger;

    public NFSeDanfseLocalRenderer(ILogger<NFSeDanfseLocalRenderer> logger)
    {
        _logger = logger;
    }

    public byte[]? TentarGerar(
        NotaFiscalServico? nota,
        bool homologacao,
        bool cancelada = false)
    {
        if (nota is null)
            return null;

        try
        {
            var config = CriarConfig(homologacao, cancelada);
            var pdf = OpenDANFSeNacional.GerarPDF(nota, config);
            return pdf is { Length: > 0 } ? pdf : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar DANFSe local a partir do objeto NotaFiscalServico.");
            return null;
        }
    }

    public byte[]? TentarGerarDeXml(
        string? xmlOuPayload,
        bool homologacao,
        bool cancelada = false)
    {
        var xml = NormalizarXmlNfse(xmlOuPayload);
        if (xml is null)
            return null;

        try
        {
            var nota = NotaFiscalServico.Load(xml);
            return TentarGerar(nota, homologacao, cancelada);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar XML NFS-e para DANFSe local.");
            return null;
        }
    }

    private static DANFSeNacionalConfig CriarConfig(bool homologacao, bool cancelada) => new()
    {
        Homologacao = homologacao,
        Cancelada = cancelada,
        ExibirQRCode = true,
        ExibirCanhoto = false
    };

    /// <summary>
    /// Aceita XML cru, envelope com &lt;NFSe&gt;, ou payload Base64 (opcionalmente GZip) como no ADN.
    /// </summary>
    internal static string? NormalizarXmlNfse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (ContemRaizNfse(s))
            return ExtrairXmlNfse(s) ?? s;

        try
        {
            var bytes = Convert.FromBase64String(s);
            if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                using var input = new MemoryStream(bytes);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip, Encoding.UTF8);
                s = reader.ReadToEnd();
            }
            else
            {
                s = Encoding.UTF8.GetString(bytes);
            }

            if (ContemRaizNfse(s))
                return ExtrairXmlNfse(s) ?? s;
        }
        catch
        {
            // não era Base64 / GZip utilizável
        }

        return null;
    }

    private static bool ContemRaizNfse(string xml) =>
        xml.Contains("<NFSe", StringComparison.OrdinalIgnoreCase);

    private static string? ExtrairXmlNfse(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var nfse = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName.Equals("NFSe", StringComparison.OrdinalIgnoreCase));
            return nfse?.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            var start = xml.IndexOf("<NFSe", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return null;
            var endTag = "</NFSe>";
            var end = xml.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
                return xml[start..];
            return xml[start..(end + endTag.Length)];
        }
    }
}
