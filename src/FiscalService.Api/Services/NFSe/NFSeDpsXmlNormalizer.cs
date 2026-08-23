using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.NFSe.Nacional.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;

namespace FiscalService.Api.Services.NFSe;

/// <summary>
/// Corrige artefatos do OpenAC na DPS (xmlns="" em infDPS/infPedReg, assinatura vazia duplicada, encoding UTF-16).
/// </summary>
internal static class NFSeDpsXmlNormalizer
{
    private static readonly Regex EmptySignatureElement = new(
        @"<Signature\b[^>]*>\s*</Signature>",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Utf16Declaration = new(
        @"encoding=""utf-16""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InfSignedEmptyXmlns = new(
        @"(<inf(?:DPS|PedReg)\b[^>]*)\s+xmlns=""""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void AssinarDps(Dps dps, ConfiguracaoNFSe configuracao, X509Certificate2 certificado)
    {
        LimparAssinaturaVazia(dps);
        dps.GerarId();

        var options = ObterSaveOptions(configuracao);
        var xmlCorrigido = RemoverXmlnsVazioInfAssinado(dps.GetXml(options, Encoding.UTF8));
        var assinado = XmlSigning.AssinarXml(
            xmlCorrigido,
            "DPS",
            "infDPS",
            "Id",
            certificado,
            comments: false,
            identado: false,
            showDeclaration: true,
            SignDigest.SHA1);

        DefinirXml(dps, AjustarXmlAposAssinatura(assinado));
    }

    public static void AssinarEvento(PedidoRegistroEvento evento, ConfiguracaoNFSe configuracao, X509Certificate2 certificado)
    {
        LimparAssinaturaVazia(evento);

        var options = ObterSaveOptions(configuracao);
        var xmlCorrigido = RemoverXmlnsVazioInfAssinado(evento.GetXml(options, Encoding.UTF8));
        var assinado = XmlSigning.AssinarXml(
            xmlCorrigido,
            "pedRegEvento",
            "infPedReg",
            "Id",
            certificado,
            comments: false,
            identado: false,
            showDeclaration: true,
            SignDigest.SHA1);

        DefinirXml(evento, AjustarXmlAposAssinatura(assinado));
    }

    internal static string RemoverXmlnsVazioInfAssinado(string xml) =>
        InfSignedEmptyXmlns.Replace(xml, "$1");

    internal static string AjustarXmlAposAssinatura(string xml)
    {
        var ajustado = EmptySignatureElement.Replace(xml, string.Empty);
        return Utf16Declaration.Replace(ajustado, "encoding=\"UTF-8\"");
    }

    private static DFeSaveOptions ObterSaveOptions(ConfiguracaoNFSe configuracao) =>
        configuracao.Geral.RetirarAcentos ? DFeSaveOptions.RemoveAccents : DFeSaveOptions.None;

    private static void LimparAssinaturaVazia<TDocument>(DFeSignDocument<TDocument> documento)
        where TDocument : class
    {
        var prop = typeof(DFeSignDocument<TDocument>).GetProperty(
            nameof(DFeSignDocument<TDocument>.Signature),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (prop?.GetValue(documento) is not DFeSignature signature)
            return;

        if (string.IsNullOrWhiteSpace(signature.SignatureValue))
            prop.SetValue(documento, null);
    }

    private static void DefinirXml<TDocument>(DFeDocument<TDocument> documento, string xml)
        where TDocument : class
    {
        var prop = typeof(DFeDocument<TDocument>).GetProperty(
            nameof(DFeDocument<TDocument>.Xml),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Propriedade Xml não encontrada no documento DFe.");

        prop.SetValue(documento, xml);
    }
}
