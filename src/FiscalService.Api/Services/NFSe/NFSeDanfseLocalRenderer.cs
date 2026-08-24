using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.DANFSe.PDFSharp;
using OpenAC.Net.NFSe.Nacional.DANFSe.PDFSharp.Common;
using OpenAC.Net.NFSe.Nacional.DANFSe.PDFSharp.Configuracao;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FiscalService.Api.Services.NFSe;

/// <summary>
/// Gera DANFSe em PDF a partir do XML autorizado (NT 008 / layout nacional),
/// via <c>OpenAC.Net.NFSe.Nacional.DANFSe.PDFSharp</c> — sem depender da API ADN de PDF.
/// </summary>
public sealed class NFSeDanfseLocalRenderer
{
    private static readonly Lazy<byte[]?> LogoNacionalBytes = new(CarregarLogoNacional);

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
            if (pdf is null or { Length: 0 })
                return null;

            // Ajustes ao layout do portal: logo com alpha + canhoto no rodapé.
            return FinalizarPdfPortal(pdf, nota);
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

    private static DANFSeNacionalConfig CriarConfig(bool homologacao, bool cancelada) =>
        // Não passa LogoNacional à OpenAC: a lib pinta fundo branco atrás da imagem.
        // O logo transparente é desenhado em FinalizarPdfPortal.
        new()
        {
            Homologacao = homologacao,
            Cancelada = cancelada,
            ExibirQRCode = true,
            // Canhoto nativo fica no topo; o portal usa rodapé (FinalizarPdfPortal).
            ExibirCanhoto = false,
            MargemVerticalMm = 12,
            MargemHorizontalMm = 3
        };

    /// <summary>
    /// Desenha logo NFS-e (PNG com alpha) no cabeçalho e canhoto no rodapé, como no portal.
    /// </summary>
    private byte[] FinalizarPdfPortal(byte[] pdfBytes, NotaFiscalServico nota)
    {
        DANFSeFontResolver.GarantirInicializacao();

        using var input = new MemoryStream(pdfBytes);
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var page = doc.Pages[0];

        using (var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
        {
            DesenharLogoCabecalho(gfx);
            DesenharCanhotoRodape(gfx, page, nota);
        }

        using var output = new MemoryStream();
        doc.Save(output, closeStream: false);
        return output.ToArray();
    }

    private void DesenharLogoCabecalho(XGraphics gfx)
    {
        var logoBytes = LogoNacionalBytes.Value;
        if (logoBytes is null or { Length: 0 })
        {
            _logger.LogWarning("Logo NFS-e Nacional não encontrado; DANFSe sem logotipo.");
            return;
        }

        try
        {
            using var logoStream = new MemoryStream(logoBytes);
            using var img = XImage.FromStream(logoStream);

            var margemH = MmToPt(3);
            var margemV = MmToPt(12);
            var headerH = MmToPt(DANFSeConstantes.AlturaCabecalhoMm);
            var logoH = MmToPt(9.2);
            var logoW = logoH * img.PixelWidth / (double)Math.Max(1, img.PixelHeight);
            var maxW = MmToPt(48);
            if (logoW > maxW)
            {
                logoH *= maxW / logoW;
                logoW = maxW;
            }

            var x = margemH + MmToPt(0.8);
            var y = margemV + Math.Max(0, (headerH - logoH) / 2);
            gfx.DrawImage(img, x, y, logoW, logoH);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao desenhar logo NFS-e Nacional no DANFSe.");
        }
    }

    private static void DesenharCanhotoRodape(XGraphics gfx, PdfPage page, NotaFiscalServico nota)
    {
        const double margin = 8.5;
        const double boxHeight = 20.5;
        var y = page.Height.Point - margin - boxHeight;
        var usable = page.Width.Point - (margin * 2);
        var col1 = usable * 0.25;
        var col2 = usable * 0.25;
        var col3 = usable - col1 - col2;

        var pen = new XPen(XColors.Black, 0.6);
        var fontLabel = new XFont(DANFSeConstantes.FontePadrao, 5.5, XFontStyleEx.Bold);
        var fontValue = new XFont(DANFSeConstantes.FontePadrao, 7, XFontStyleEx.Regular);

        var x = margin;
        DesenharCelulaCanhoto(gfx, x, y, col1, boxHeight, pen, fontLabel, fontValue,
            "DATA CIENTIFICAÇÃO:", string.Empty);
        x += col1;
        DesenharCelulaCanhoto(gfx, x, y, col2, boxHeight, pen, fontLabel, fontValue,
            "IDENTIFICAÇÃO E ASSINATURA", string.Empty);
        x += col2;

        var numero = nota.Informacoes?.NumeroNFSe.ToString() ?? string.Empty;
        var chave = ExtrairChaveAcesso(nota);
        var valorChave = string.IsNullOrWhiteSpace(chave)
            ? numero
            : string.IsNullOrWhiteSpace(numero) ? chave : $"{numero} / {chave}";

        DesenharCelulaCanhoto(gfx, x, y, col3, boxHeight, pen, fontLabel, fontValue,
            "Nº NFS-e / CHAVE NFS-e", valorChave);
    }

    private static void DesenharCelulaCanhoto(
        XGraphics gfx,
        double x,
        double y,
        double width,
        double height,
        XPen pen,
        XFont fontLabel,
        XFont fontValue,
        string label,
        string value)
    {
        gfx.DrawRectangle(pen, x, y, width, height);
        gfx.DrawString(label, fontLabel, XBrushes.Black,
            new XRect(x + 2, y + 1.5, width - 4, 8),
            XStringFormats.TopLeft);
        if (!string.IsNullOrWhiteSpace(value))
        {
            gfx.DrawString(value, fontValue, XBrushes.Black,
                new XRect(x + 2, y + 9, width - 4, height - 10),
                XStringFormats.TopLeft);
        }
    }

    private static string ExtrairChaveAcesso(NotaFiscalServico nota)
    {
        var id = nota.Informacoes?.Id?.Trim() ?? string.Empty;
        if (id.StartsWith("NFS", StringComparison.OrdinalIgnoreCase))
            id = id[3..];

        if (id.Length == 50 && id.All(char.IsDigit))
            return id;

        var digits = new string(id.Where(char.IsDigit).ToArray());
        return digits.Length >= 44 ? digits : id;
    }

    private static double MmToPt(double mm) => mm * 72.0 / 25.4;

    private static byte[]? CarregarLogoNacional()
    {
        // PNG já vem composto sobre cinza do cabeçalho (#F3F3F3): PDFSharp/PDFium
        // flatten alpha em branco e gerava "caixa" no header cinza.
        var baseDir = AppContext.BaseDirectory;
        var pathArquivo = Path.Combine(baseDir, "Resources", "nfse", "logo-nfse-nacional.png");
        if (File.Exists(pathArquivo))
            return File.ReadAllBytes(pathArquivo);

        var asm = typeof(NFSeDanfseLocalRenderer).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("logo-nfse-nacional.png", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return null;

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

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
