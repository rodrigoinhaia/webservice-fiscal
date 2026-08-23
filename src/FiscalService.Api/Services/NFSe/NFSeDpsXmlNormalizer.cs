using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.NFSe.Nacional.Common.Model;

namespace FiscalService.Api.Services.NFSe;

/// <summary>
/// Corrige artefatos do OpenAC na serialização/assinatura da DPS (assinatura vazia duplicada, xmlns="" em infDPS, encoding UTF-16).
/// </summary>
internal static class NFSeDpsXmlNormalizer
{
    private static readonly XNamespace DsigNs = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly Regex XmlDeclarationEncoding = new(
        @"<\?xml version=""1\.0"" encoding=""[^""]+""(\s+standalone=""[^""]+"")?\?>",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EmptyXmlnsAttribute = new(
        @"\s+xmlns=""""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
        var nfseNs = root.Name.Namespace;

        foreach (var infDps in root.Descendants().Where(e => e.Name.LocalName == "infDPS").ToList())
        {
            CorrigirNamespaceInfDps(infDps, nfseNs);
            CorrigirNamespacesDescendentes(infDps, nfseNs);
        }

        foreach (var sig in root.Descendants(DsigNs + "Signature").ToList())
        {
            if (AssinaturaInvalida(sig))
                sig.Remove();
        }

        return SerializarUtf8(doc);
    }

    private static void CorrigirNamespaceInfDps(XElement infDps, XNamespace nfseNs)
    {
        if (!string.IsNullOrEmpty(infDps.Name.NamespaceName))
            return;

        var attrs = infDps.Attributes()
            .Where(a => !a.IsNamespaceDeclaration)
            .Select(a => string.IsNullOrEmpty(a.Name.NamespaceName)
                ? new XAttribute(a.Name.LocalName, a.Value)
                : new XAttribute(a.Name, a.Value));

        infDps.ReplaceWith(new XElement(nfseNs + "infDPS", attrs, infDps.Nodes()));
    }

    private static void CorrigirNamespacesDescendentes(XElement pai, XNamespace nfseNs)
    {
        foreach (var child in pai.Elements().ToList())
        {
            if (child.Name.Namespace == DsigNs)
                continue;

            if (string.IsNullOrEmpty(child.Name.NamespaceName))
            {
                var attrs = child.Attributes()
                    .Where(a => !a.IsNamespaceDeclaration)
                    .Select(a => string.IsNullOrEmpty(a.Name.NamespaceName)
                        ? new XAttribute(a.Name.LocalName, a.Value)
                        : new XAttribute(a.Name, a.Value));
                var novo = new XElement(nfseNs + child.Name.LocalName, attrs, child.Nodes());
                child.ReplaceWith(novo);
                CorrigirNamespacesDescendentes(novo, nfseNs);
            }
            else
            {
                CorrigirNamespacesDescendentes(child, nfseNs);
            }
        }
    }

    private static string SerializarUtf8(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
            Indent = false,
            NewLineHandling = NewLineHandling.None
        };

        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, settings))
            doc.Save(writer);

        var result = Encoding.UTF8.GetString(ms.ToArray());
        result = XmlDeclarationEncoding.Replace(
            result,
            match => match.Groups[1].Success
                ? $"<?xml version=\"1.0\" encoding=\"UTF-8\"{match.Groups[1].Value}?>"
                : "<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        return EmptyXmlnsAttribute.Replace(result, string.Empty);
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
