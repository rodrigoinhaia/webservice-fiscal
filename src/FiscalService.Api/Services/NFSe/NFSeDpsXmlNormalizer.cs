using System.Reflection;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.NFSe.Nacional.Common.Model;

namespace FiscalService.Api.Services.NFSe;

/// <summary>
/// Corrige artefatos do OpenAC na serialização/assinatura da DPS (assinatura vazia duplicada, xmlns="" em infDPS).
/// </summary>
internal static class NFSeDpsXmlNormalizer
{
    private static readonly XNamespace DsigNs = "http://www.w3.org/2000/09/xmldsig#";

    public static void NormalizarDpsAposAssinatura(Dps dps)
    {
        if (string.IsNullOrWhiteSpace(dps.Xml))
            return;

        DefinirXml(dps, NormalizarXml(dps.Xml));
    }

    public static void NormalizarEventoAposAssinatura(PedidoRegistroEvento evento)
    {
        if (string.IsNullOrWhiteSpace(evento.Xml))
            return;

        DefinirXml(evento, NormalizarXml(evento.Xml));
    }

    internal static string NormalizarXml(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var root = doc.Root ?? throw new InvalidOperationException("XML sem elemento raiz.");

        foreach (var infDps in root.Descendants().Where(e => e.Name.LocalName == "infDPS"))
        {
            foreach (var attr in infDps.Attributes().Where(a => a.IsNamespaceDeclaration && string.IsNullOrEmpty(a.Value)).ToList())
                attr.Remove();

            var xmlns = infDps.Attribute("xmlns");
            if (xmlns is { Value: "" })
                xmlns.Remove();
        }

        foreach (var sig in root.Descendants(DsigNs + "Signature").ToList())
        {
            if (AssinaturaInvalida(sig))
                sig.Remove();
        }

        using var writer = new StringWriter();
        doc.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    private static bool AssinaturaInvalida(XElement sig)
    {
        var sigValue = sig.Element(DsigNs + "SignatureValue")?.Value;
        var digest = sig.Descendants(DsigNs + "DigestValue").FirstOrDefault()?.Value;
        return string.IsNullOrWhiteSpace(sigValue) || string.IsNullOrWhiteSpace(digest);
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
