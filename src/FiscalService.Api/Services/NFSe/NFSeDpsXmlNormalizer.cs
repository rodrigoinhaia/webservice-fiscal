using System.Reflection;
using System.Text.RegularExpressions;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.NFSe.Nacional.Common.Model;

namespace FiscalService.Api.Services.NFSe;

/// <summary>
/// Corrige artefatos do OpenAC na DPS sem invalidar a assinatura XMLDSig.
/// Alterações estruturais só antes de assinar; após assinar, apenas ajustes em string fora do infDPS assinado.
/// </summary>
internal static class NFSeDpsXmlNormalizer
{
    private static readonly Regex EmptySignatureElement = new(
        @"<Signature\b[^>]*>\s*</Signature>",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Utf16Declaration = new(
        @"encoding=""utf-16""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void PrepararDpsAntesAssinatura(Dps dps) => LimparAssinaturaVazia(dps);

    public static void PrepararEventoAntesAssinatura(PedidoRegistroEvento evento) => LimparAssinaturaVazia(evento);

    public static void NormalizarDpsAposAssinatura(Dps dps)
    {
        if (string.IsNullOrWhiteSpace(dps.Xml))
            return;

        DefinirXml(dps, AjustarXmlAposAssinatura(dps.Xml));
    }

    public static void NormalizarEventoAposAssinatura(PedidoRegistroEvento evento)
    {
        if (string.IsNullOrWhiteSpace(evento.Xml))
            return;

        DefinirXml(evento, AjustarXmlAposAssinatura(evento.Xml));
    }

    internal static string AjustarXmlAposAssinatura(string xml)
    {
        var ajustado = EmptySignatureElement.Replace(xml, string.Empty);
        return Utf16Declaration.Replace(ajustado, "encoding=\"UTF-8\"");
    }

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
