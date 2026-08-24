using System.Xml.Linq;
using DFe.Utils;
using NFe.Classes;
using NFe.Classes.Protocolo;
using NFe.Utils;

namespace FiscalService.Api.Services.Danfe;

/// <summary>Monta e normaliza XML <c>nfeProc</c> para DANFE e persistência.</summary>
public static class NFeProcComposer
{
    private const string NfeNs = "http://www.portalfiscal.inf.br/nfe";

    public static string MontarDeAutorizacao(NFe.Classes.NFe nfe, protNFe protocolo)
    {
        var proc = new nfeProc
        {
            versao = nfe.infNFe?.versao ?? "4.00",
            NFe = nfe,
            protNFe = protocolo
        };
        return proc.ObterXmlString();
    }

    /// <summary>
    /// Aceita <c>nfeProc</c>, <c>NFe</c> isolada ou re-serializa XML já válido.
    /// Rejeita <c>retEnviNFe</c> (só protocolo, sem corpo da nota).
    /// </summary>
    public static string NormalizarParaDanfe(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML vazio.", nameof(xml));

        var trimmed = xml.Trim();
        var root = XDocument.Parse(trimmed).Root
            ?? throw new InvalidOperationException("XML sem elemento raiz.");

        return root.Name.LocalName switch
        {
            "nfeProc" => trimmed,
            "NFe" => trimmed,
            "retEnviNFe" => throw new InvalidOperationException(
                "XML retEnviNFe não contém o corpo da NF-e. Informe nfeProc ou emita novamente para obter xmlAutorizado completo."),
            _ => throw new InvalidOperationException(
                $"Raiz XML '{root.Name.LocalName}' não suportada para DANFE. Esperado nfeProc ou NFe.")
        };
    }

    public static nfeProc CarregarProc(string xmlNfeProc)
    {
        var normalizado = NormalizarParaDanfe(xmlNfeProc);
        return FuncoesXml.XmlStringParaClasse<nfeProc>(normalizado);
    }
}
