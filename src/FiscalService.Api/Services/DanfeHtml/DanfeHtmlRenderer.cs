using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FiscalService.Api.Services.DanfeHtml;

/// <summary>
/// Monta HTML imprimível a partir do XML <c>nfeProc</c> (NF-e / NFC-e). Cobertura ampliada de campos do leiaute
/// para conferência e impressão via navegador (Ctrl+P → PDF). Não substitui 100% do desenho gráfico oficial do MOC.
/// </summary>
public static class DanfeHtmlRenderer
{
    private const string NfeNs = "http://www.portalfiscal.inf.br/nfe";

    private static readonly string CssDanfeNfe = """
<style>
body { font-family: Segoe UI, Arial, sans-serif; margin: 12px 16px; color: #111; font-size: 0.82rem; }
h1 { font-size: 1.2rem; margin: 0 0 6px; }
h2 { font-size: 0.95rem; margin: 14px 0 6px; border-bottom: 2px solid #333; padding-bottom: 2px; }
h3 { font-size: 0.88rem; margin: 10px 0 4px; color: #333; }
table { width: 100%; border-collapse: collapse; margin-top: 6px; }
th, td { border: 1px solid #333; padding: 4px 6px; vertical-align: top; }
th { background: #e8e8e8; text-align: left; font-weight: 600; }
.num { text-align: right; white-space: nowrap; }
.meta { font-size: 0.78rem; color: #444; margin: 2px 0; }
.chave { font-family: Consolas, monospace; letter-spacing: 0.08em; font-size: 0.72rem; word-break: break-all; }
.homologacao { background: #fee; border: 2px solid #c00; color: #800; text-align: center; font-weight: bold; padding: 10px; margin-bottom: 12px; }
.footer { margin-top: 20px; font-size: 0.72rem; color: #555; border-top: 1px solid #999; padding-top: 8px; }
.kv td:first-child { width: 28%; font-weight: 600; background: #f5f5f5; white-space: nowrap; }
.kv td:last-child { word-break: break-word; }
.imposto-resumo { font-size: 0.72rem; line-height: 1.35; max-width: 420px; }
.nowrap { white-space: nowrap; }
@media print {
  body { margin: 6px; font-size: 0.75rem; }
  .no-print { display: none; }
  a { color: #000; text-decoration: none; }
}
</style>
""";

    private static readonly string CssDanfeNfce = """
<style>
body { font-family: Segoe UI, Arial, sans-serif; margin: 10px 12px; max-width: 520px; color: #111; font-size: 0.8rem; }
h1 { font-size: 1.05rem; margin: 0 0 6px; }
h2 { font-size: 0.9rem; margin: 12px 0 5px; border-bottom: 2px solid #333; }
h3 { font-size: 0.82rem; margin: 8px 0 3px; color: #333; }
table { width: 100%; border-collapse: collapse; margin-top: 4px; font-size: 0.76rem; }
th, td { border: 1px solid #333; padding: 3px 5px; vertical-align: top; }
th { background: #f0f0f0; }
.num { text-align: right; }
.meta { font-size: 0.72rem; color: #444; }
.chave { font-family: Consolas, monospace; font-size: 0.65rem; word-break: break-all; }
.qr { font-size: 0.62rem; word-break: break-all; }
.homologacao { background: #fee; border: 2px solid #c00; color: #800; text-align: center; font-weight: bold; padding: 8px; margin-bottom: 10px; }
.kv td:first-child { width: 34%; font-weight: 600; background: #f7f7f7; }
.imposto-resumo { font-size: 0.68rem; line-height: 1.3; }
.footer { margin-top: 14px; font-size: 0.68rem; color: #555; border-top: 1px solid #999; padding-top: 6px; }
@media print {
  .no-print { display: none; }
  body { max-width: none; }
  a { color: #000; text-decoration: none; }
}
</style>
""";

    public static string GerarNFe(string xmlNfeProc)
    {
        var doc = CarregarXmlSeguro(xmlNfeProc);
        var ctx = ResolverContexto(doc);
        if (ctx.InfNFe is null)
            throw new InvalidOperationException("XML inválido: não foi possível localizar infNFe no nfeProc.");

        return MontarHtmlNFe(ctx);
    }

    public static string GerarNFCe(string xmlNfeProc)
    {
        var doc = CarregarXmlSeguro(xmlNfeProc);
        var ctx = ResolverContexto(doc);
        if (ctx.InfNFe is null)
            throw new InvalidOperationException("XML inválido: não foi possível localizar infNFe no nfeProc.");

        return MontarHtmlNFCe(ctx);
    }

    private static XDocument CarregarXmlSeguro(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML vazio.", nameof(xml));

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        using var reader = XmlReader.Create(new StringReader(xml.Trim()), settings);
        return XDocument.Load(reader);
    }

    private static DanfeXmlContext ResolverContexto(XDocument doc)
    {
        var root = doc.Root;
        if (root is null)
            throw new InvalidOperationException("XML sem elemento raiz.");

        XElement? nfe = null;
        if (root.Name.LocalName == "nfeProc")
            nfe = Filho(root, "NFe");
        else if (root.Name.LocalName == "NFe")
            nfe = root;

        var infNFe = nfe is not null ? Filho(nfe, "infNFe") : null;
        XElement? prot = null;
        if (root.Name.LocalName == "nfeProc")
            prot = Filho(root, "protNFe");

        var infProt = prot is not null ? FilhoDesc(prot, "infProt") : null;
        var infNFeSupl = nfe is not null ? Filho(nfe, "infNFeSupl") : null;

        return new DanfeXmlContext(root, nfe, infNFe, infProt, infNFeSupl);
    }

    private static string MontarHtmlNFe(DanfeXmlContext ctx)
    {
        var inf = ctx.InfNFe!;
        var ide = Filho(inf, "ide");
        var emit = Filho(inf, "emit");
        var dest = Filho(inf, "dest");
        var total = Filho(inf, "total");
        var icmsTot = total is not null ? Filho(total, "ICMSTot") : null;
        var issqn = total is not null ? Filho(total, "ISSQNtot") : null;
        var retTrib = total is not null ? Filho(total, "retTrib") : null;
        var transp = Filho(inf, "transp");
        var cobr = Filho(inf, "cobr");
        var pag = Filho(inf, "pag");
        var infAdic = Filho(inf, "infAdic");
        var retirada = Filho(inf, "retirada");
        var entrega = Filho(inf, "entrega");
        var compra = Filho(inf, "compra");
        var exporta = Filho(inf, "exporta");
        var autXmlList = inf.Elements().Where(e => e.Name.LocalName == "autXML").ToList();

        var tpAmb = Texto(ide, "tpAmb");
        var homologacao = tpAmb == "2";
        var chave = ExtrairChaveAcesso(inf);
        var bannerHom = homologacao
            ? "<div class=\"homologacao\">SEM VALOR FISCAL — HOMOLOGAÇÃO</div>"
            : "";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.Append("<title>DANFE NF-e ").Append(Esc(Texto(ide, "nNF"))).AppendLine("</title>");
        sb.AppendLine(CssDanfeNfe);
        sb.AppendLine("</head><body>");
        sb.AppendLine(bannerHom);
        sb.AppendLine("<p class=\"no-print meta\">Use <strong>Ctrl+P</strong> (ou Cmd+P) para imprimir ou salvar em PDF pelo navegador.</p>");
        sb.AppendLine("<h1>DANFE — Documento Auxiliar da Nota Fiscal Eletrônica</h1>");

        sb.AppendLine("<h2>Dados da NF-e (identificação)</h2>");
        sb.Append(MontarTabelaIde(ide, chave));
        if (ide is not null)
            sb.Append(MontarNfRefs(ide));
        if (autXmlList.Count > 0)
        {
            sb.AppendLine("<h3>Autorizados a acessar o XML (autXML)</h3><ul>");
            foreach (var ax in autXmlList)
            {
                var cnpj = Texto(ax, "CNPJ");
                var cpf = Texto(ax, "CPF");
                sb.Append("<li>").Append(Esc(FormatarCnpjCpf(cnpj ?? cpf) ?? cnpj ?? cpf ?? "—")).AppendLine("</li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("<h2>Emitente</h2>");
        sb.Append(MontarBlocoPessoa("Emitente", emit, Filho(emit, "enderEmit")));

        sb.AppendLine("<h2>Destinatário / Remetente</h2>");
        sb.Append(MontarBlocoPessoa("Destinatário", dest, Filho(dest, "enderDest")));

        if (retirada is not null || entrega is not null)
        {
            sb.AppendLine("<h2>Retirada / Entrega</h2>");
            if (retirada is not null)
                sb.Append(MontarBlocoEnderecoTitulo("Local de retirada", retirada));
            if (entrega is not null)
                sb.Append(MontarBlocoEnderecoTitulo("Local de entrega", entrega));
        }

        sb.AppendLine("<h2>Autorização de uso (SEFAZ)</h2>");
        sb.Append(MontarProtocolo(ctx.InfProt));

        sb.AppendLine("<h2>Discriminação dos produtos e serviços</h2>");
        sb.Append(MontarTabelaItensNFe(inf));

        sb.AppendLine("<h2>Totais da nota (ICMS / valores)</h2>");
        sb.Append(MontarTotaisIcms(icmsTot));
        if (issqn is not null)
        {
            sb.AppendLine("<h3>Totais ISSQN</h3>");
            sb.Append(MontarTabelaGenerica(issqn));
        }
        if (retTrib is not null)
        {
            sb.AppendLine("<h3>Retenções tributárias</h3>");
            sb.Append(MontarTabelaGenerica(retTrib));
        }

        if (cobr is not null)
        {
            sb.AppendLine("<h2>Cobrança / fatura e duplicatas</h2>");
            sb.Append(MontarCobranca(cobr));
        }

        if (transp is not null)
        {
            sb.AppendLine("<h2>Transporte / volumes</h2>");
            sb.Append(MontarTransporte(transp));
        }

        if (pag is not null)
        {
            sb.AppendLine("<h2>Pagamentos</h2>");
            sb.Append(MontarPagamento(pag));
        }

        if (compra is not null || exporta is not null)
        {
            sb.AppendLine("<h2>Compra / Exportação</h2>");
            if (compra is not null)
                sb.Append(MontarTabelaGenerica(compra, "Compra"));
            if (exporta is not null)
                sb.Append(MontarTabelaGenerica(exporta, "Exportação"));
        }

        sb.AppendLine("<h2>Informações complementares</h2>");
        sb.Append(MontarInfAdic(infAdic));

        sb.AppendLine("<div class=\"footer\">");
        sb.AppendLine("Documento gerado a partir do XML <code>nfeProc</code> autorizado. Conferir sempre o XML e a legislação vigente. ");
        sb.AppendLine("Campos opcionais ausentes no XML não aparecem nas tabelas acima.");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string MontarHtmlNFCe(DanfeXmlContext ctx)
    {
        var inf = ctx.InfNFe!;
        var ide = Filho(inf, "ide");
        var emit = Filho(inf, "emit");
        var dest = Filho(inf, "dest");
        var total = Filho(inf, "total");
        var icmsTot = total is not null ? Filho(total, "ICMSTot") : null;
        var pag = Filho(inf, "pag");
        var infAdic = Filho(inf, "infAdic");
        var transp = Filho(inf, "transp");

        var tpAmb = Texto(ide, "tpAmb");
        var homologacao = tpAmb == "2";
        var chave = ExtrairChaveAcesso(inf);
        var bannerHom = homologacao
            ? "<div class=\"homologacao\">SEM VALOR FISCAL — HOMOLOGAÇÃO</div>"
            : "";

        var qr = NormalizarCdata(Texto(ctx.InfNFeSupl, "qrCode"));
        var urlChave = Texto(ctx.InfNFeSupl, "urlChave")?.Trim();
        var qrBlock = MontarBlocoQr(qr, urlChave);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.Append("<title>NFC-e ").Append(Esc(Texto(ide, "nNF"))).AppendLine("</title>");
        sb.AppendLine(CssDanfeNfce);
        sb.AppendLine("</head><body>");
        sb.AppendLine(bannerHom);
        sb.AppendLine("<p class=\"no-print meta\"><strong>Ctrl+P</strong> para imprimir ou salvar PDF.</p>");
        sb.AppendLine("<h1>NFC-e — Nota Fiscal do Consumidor Eletrônica</h1>");

        sb.AppendLine("<h2>Identificação</h2>");
        sb.Append(MontarTabelaIdeNfce(ide, chave));

        sb.AppendLine("<h2>Emitente</h2>");
        sb.Append(MontarBlocoPessoa("Emitente", emit, Filho(emit, "enderEmit")));

        sb.AppendLine("<h2>Consumidor</h2>");
        sb.Append(MontarBlocoPessoa("Destinatário", dest, Filho(dest, "enderDest")));

        sb.AppendLine("<h2>Chave de acesso</h2>");
        sb.Append("<p class=\"chave\">").Append(Esc(chave)).AppendLine("</p>");
        sb.Append(qrBlock);

        sb.AppendLine("<h2>Itens</h2>");
        sb.Append(MontarTabelaItensNFCe(inf));

        sb.AppendLine("<h2>Totais</h2>");
        sb.Append(MontarTotaisIcms(icmsTot));

        if (pag is not null)
        {
            sb.AppendLine("<h2>Pagamentos</h2>");
            sb.Append(MontarPagamento(pag));
        }

        if (transp is not null)
        {
            sb.AppendLine("<h2>Transporte</h2>");
            sb.Append(MontarTransporte(transp));
        }

        sb.AppendLine("<h2>Informações complementares</h2>");
        sb.Append(MontarInfAdic(infAdic));

        sb.AppendLine("<h2>Autorização (SEFAZ)</h2>");
        sb.Append(MontarProtocolo(ctx.InfProt));

        sb.AppendLine("<div class=\"footer\">");
        sb.AppendLine("Cupom / NFC-e gerado a partir do <code>nfeProc</code>. Validar impressão e regras da SEFAZ-UF.");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string? NormalizarCdata(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var qr = s.Trim();
        if (qr.StartsWith("<![CDATA[", StringComparison.Ordinal))
        {
            var end = qr.IndexOf("]]>", StringComparison.Ordinal);
            if (end > 9)
                qr = qr[9..end].Trim();
        }
        return qr;
    }

    private static string MontarBlocoQr(string? qr, string? urlChave)
    {
        var safeQr = SafeHttpUrl(qr);
        var sb = new StringBuilder();
        if (string.IsNullOrWhiteSpace(qr))
            sb.AppendLine("<p class=\"meta\">QR Code não presente no XML.</p>");
        else if (safeQr is not null)
            sb.Append("<p class=\"meta\"><a href=\"").Append(Esc(safeQr)).Append("\">Link de consulta (QR)</a></p><p class=\"qr\">")
                .Append(Esc(qr)).AppendLine("</p>");
        else
            sb.Append("<p class=\"qr\">").Append(Esc(qr)).AppendLine("</p>");

        if (!string.IsNullOrWhiteSpace(urlChave))
        {
            var safeUrl = SafeHttpUrl(urlChave);
            if (safeUrl is not null)
                sb.Append("<p class=\"meta\">Consulta: <a href=\"").Append(Esc(safeUrl)).Append("\">")
                    .Append(Esc(urlChave)).AppendLine("</a></p>");
            else
                sb.Append("<p class=\"meta\">Consulta: ").Append(Esc(urlChave)).AppendLine("</p>");
        }
        return sb.ToString();
    }

    private static string MontarNfRefs(XElement ide)
    {
        var refs = ide.Elements().Where(e => e.Name.LocalName == "NFref").ToList();
        if (refs.Count == 0) return "";
        var sb = new StringBuilder();
        sb.AppendLine("<h3>Documentos referenciados (NFref)</h3>");
        sb.AppendLine("<table><thead><tr><th>refNFe</th><th>refNF</th><th>refNFP</th><th>refECF</th><th>refCTe</th></tr></thead><tbody>");
        foreach (var r in refs)
        {
            var refNFe = Texto(r, "refNFe");
            var refNf = Filho(r, "refNF");
            var refNfp = Filho(r, "refNFP");
            var refEcf = Filho(r, "refECF");
            var refCte = Texto(r, "refCTe");
            var refNfStr = refNf is null ? null : Juntar(Texto(refNf, "cUF"), Texto(refNf, "AAMM"), Texto(refNf, "CNPJ"), Texto(refNf, "mod"), Texto(refNf, "serie"), Texto(refNf, "nNF"));
            var refNfpStr = refNfp is null ? null : Juntar(Texto(refNfp, "cUF"), Texto(refNfp, "AAMM"), Texto(refNfp, "CNPJ"), Texto(refNfp, "CPF"), Texto(refNfp, "IE"), Texto(refNfp, "mod"), Texto(refNfp, "serie"), Texto(refNfp, "nNF"));
            var refEcfStr = refEcf is null ? null : Juntar(Texto(refEcf, "mod"), Texto(refEcf, "nECF"), Texto(refEcf, "nCOO"));
            sb.Append("<tr><td>").Append(Esc(refNFe)).Append("</td><td>").Append(Esc(refNfStr))
                .Append("</td><td>").Append(Esc(refNfpStr)).Append("</td><td>").Append(Esc(refEcfStr))
                .Append("</td><td>").Append(Esc(refCte)).AppendLine("</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        return sb.ToString();
    }

    private static string MontarTabelaIde(XElement? ide, string chave)
    {
        if (ide is null) return "<p class=\"meta\">—</p>";
        var rows = new List<(string, string?)>
        {
            ("Chave de acesso", FormatarChaveVisual(chave)),
            ("Modelo", Texto(ide, "mod")),
            ("Série", Texto(ide, "serie")),
            ("Número", Texto(ide, "nNF")),
            ("Data/hora emissão", Texto(ide, "dhEmi")),
            ("Data/hora saída", Texto(ide, "dhSaiEnt")),
            ("Tipo NF-e", Texto(ide, "tpNF")),
            ("Natureza da operação", Texto(ide, "natOp")),
            ("Finalidade", Texto(ide, "finNFe")),
            ("Consumidor final", Texto(ide, "indFinal")),
            ("Presença comprador", Texto(ide, "indPres")),
            ("Destino operação", Texto(ide, "idDest")),
            ("Município FG", Texto(ide, "cMunFG")),
            ("Tipo emissão", Texto(ide, "tpEmis")),
            ("Ambiente", Texto(ide, "tpAmb") is "1" ? "Produção (1)" : Texto(ide, "tpAmb") is "2" ? "Homologação (2)" : Texto(ide, "tpAmb")),
            ("Processo emissão", Texto(ide, "procEmi")),
            ("Versão processo", Texto(ide, "verProc")),
            ("cUF", Texto(ide, "cUF")),
            ("cNF", Texto(ide, "cNF")),
            ("Tipo impressão DANFE", Texto(ide, "tpImp")),
            ("Indicador intermediador (indIntermed)", Texto(ide, "indIntermed"))
        };
        return MontarTabelaKv(rows);
    }

    private static string MontarTabelaIdeNfce(XElement? ide, string chave)
    {
        if (ide is null) return "<p class=\"meta\">—</p>";
        var rows = new List<(string, string?)>
        {
            ("Chave", FormatarChaveVisual(chave)),
            ("Modelo", Texto(ide, "mod")),
            ("Série / Nº", $"{Texto(ide, "serie")} / {Texto(ide, "nNF")}"),
            ("Emissão", Texto(ide, "dhEmi")),
            ("Ambiente", Texto(ide, "tpAmb") is "1" ? "Produção" : Texto(ide, "tpAmb") is "2" ? "Homologação" : Texto(ide, "tpAmb")),
            ("Tipo emissão", Texto(ide, "tpEmis")),
            ("Finalidade", Texto(ide, "finNFe")),
            ("Consumidor final", Texto(ide, "indFinal")),
            ("Presença", Texto(ide, "indPres")),
            ("cUF / cNF", $"{Texto(ide, "cUF")} / {Texto(ide, "cNF")}")
        };
        return MontarTabelaKv(rows);
    }

    private static string MontarTabelaKv(IEnumerable<(string Label, string? Value)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<table class=\"kv\">");
        foreach (var (label, value) in rows)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            sb.Append("<tr><td>").Append(Esc(label)).Append("</td><td>").Append(Esc(value)).AppendLine("</td></tr>");
        }
        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static string? FormatarChaveVisual(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length != 44) return chave;
        // Agrupa de 4 em 4 para leitura
        var sb = new StringBuilder();
        for (var i = 0; i < chave.Length; i += 4)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(chave, i, Math.Min(4, chave.Length - i));
        }
        return sb.ToString();
    }

    private static string MontarBlocoPessoa(string titulo, XElement? pessoa, XElement? ender)
    {
        if (pessoa is null) return $"<p class=\"meta\">{Esc(titulo)} não informado.</p>";
        var doc = FormatarCnpjCpf(Texto(pessoa, "CNPJ") ?? Texto(pessoa, "CPF"));
        var rows = new List<(string, string?)>
        {
            ("Razão social / nome", Texto(pessoa, "xNome")),
            ("Nome fantasia", Texto(pessoa, "xFant")),
            ("CNPJ / CPF", doc),
            ("IE", Texto(pessoa, "IE")),
            ("IE do substituto", Texto(pessoa, "IEST")),
            ("IM", Texto(pessoa, "IM")),
            ("CNAE", Texto(pessoa, "CNAE")),
            ("CRT", Texto(pessoa, "CRT")),
            ("Indicador IE dest.", Texto(pessoa, "indIEDest")),
            ("E-mail", Texto(pessoa, "email"))
        };
        var sb = new StringBuilder();
        sb.Append(MontarTabelaKv(rows));
        if (ender is not null)
        {
            sb.AppendLine("<h3>Endereço</h3>");
            sb.Append(MontarEndereco(ender));
        }
        return sb.ToString();
    }

    private static string MontarBlocoEnderecoTitulo(string titulo, XElement ender)
    {
        var sb = new StringBuilder();
        sb.Append("<h3>").Append(Esc(titulo)).AppendLine("</h3>");
        var doc = FormatarCnpjCpf(Texto(ender, "CNPJ") ?? Texto(ender, "CPF"));
        if (!string.IsNullOrWhiteSpace(doc) || !string.IsNullOrWhiteSpace(Texto(ender, "xNome")))
        {
            sb.Append(MontarTabelaKv(new List<(string, string?)>
            {
                ("CNPJ/CPF", doc),
                ("Nome / IE", Juntar(Texto(ender, "xNome"), Texto(ender, "IE")))
            }));
        }
        sb.Append(MontarEndereco(ender));
        return sb.ToString();
    }

    private static string MontarEndereco(XElement ender)
    {
        var rows = new List<(string, string?)>
        {
            ("Logradouro", Juntar(Texto(ender, "xLgr"), Texto(ender, "nro"), Texto(ender, "xCpl"))),
            ("Bairro", Texto(ender, "xBairro")),
            ("Município", Juntar(Texto(ender, "cMun"), Texto(ender, "xMun"))),
            ("UF / CEP", Juntar(Texto(ender, "UF"), Texto(ender, "CEP"))),
            ("cPais / xPais", Juntar(Texto(ender, "cPais"), Texto(ender, "xPais"))),
            ("Telefone", Texto(ender, "fone"))
        };
        return MontarTabelaKv(rows);
    }

    private static string? Juntar(params string?[] partes)
    {
        var p = partes.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        return p.Length == 0 ? null : string.Join(" — ", p);
    }

    private static string MontarProtocolo(XElement? infProt)
    {
        if (infProt is null)
            return "<p class=\"meta\">Sem protocolo no XML (infProt).</p>";
        var rows = new List<(string, string?)>
        {
            ("Chave", Texto(infProt, "chNFe")),
            ("Protocolo", Texto(infProt, "nProt")),
            ("Data recebimento", Texto(infProt, "dhRecbto")),
            ("Digest", Texto(infProt, "digVal")),
            ("cStat", Texto(infProt, "cStat")),
            ("xMotivo", Texto(infProt, "xMotivo")),
            ("Ambiente", Texto(infProt, "tpAmb") is "1" ? "Produção" : Texto(infProt, "tpAmb") is "2" ? "Homologação" : Texto(infProt, "tpAmb"))
        };
        return MontarTabelaKv(rows);
    }

    private static string MontarTabelaItensNFe(XElement infNFe)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr>");
        sb.Append("<th>#</th><th>cProd</th><th>cEAN</th><th>NCM</th><th>CEST</th><th>CFOP</th>");
        sb.Append("<th>Descrição</th><th>uCom</th><th class=\"num\">Qtd</th><th class=\"num\">vUn</th>");
        sb.Append("<th class=\"num\">vProd</th><th class=\"num\">Desc</th><th class=\"num\">Frete</th><th>Impostos (resumo)</th>");
        sb.AppendLine("</tr></thead><tbody>");

        var dets = infNFe.Elements().Where(e => e.Name.LocalName == "det").OrderBy(e => e.Attribute("nItem")?.Value, StringComparer.Ordinal).ToList();
        if (dets.Count == 0)
        {
            sb.AppendLine("<tr><td colspan=\"13\">Nenhum item (det) encontrado.</td></tr>");
        }
        else
        {
            foreach (var det in dets)
            {
                var prod = Filho(det, "prod");
                var imposto = Filho(det, "imposto");
                var nItem = det.Attribute("nItem")?.Value ?? "";
                sb.Append("<tr>");
                sb.Append("<td>").Append(Esc(nItem)).Append("</td>");
                sb.Append("<td class=\"nowrap\">").Append(Esc(Texto(prod, "cProd"))).Append("</td>");
                sb.Append("<td class=\"nowrap\">").Append(Esc(Texto(prod, "cEAN"))).Append("</td>");
                sb.Append("<td class=\"nowrap\">").Append(Esc(Texto(prod, "NCM"))).Append("</td>");
                sb.Append("<td class=\"nowrap\">").Append(Esc(Texto(prod, "CEST"))).Append("</td>");
                sb.Append("<td class=\"nowrap\">").Append(Esc(Texto(prod, "CFOP"))).Append("</td>");
                sb.Append("<td>").Append(Esc(Texto(prod, "xProd"))).Append("</td>");
                sb.Append("<td>").Append(Esc(Texto(prod, "uCom"))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Esc(Texto(prod, "qCom"))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Esc(Texto(prod, "vUnCom"))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Esc(Texto(prod, "vProd"))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Esc(Texto(prod, "vDesc"))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Esc(Texto(prod, "vFrete"))).Append("</td>");
                sb.Append("<td class=\"imposto-resumo\">").Append(ResumoImposto(imposto)).Append("</td>");
                sb.AppendLine("</tr>");
                var infAdProd = Filho(det, "infAdProd");
                if (infAdProd is not null && !string.IsNullOrWhiteSpace(infAdProd.Value))
                    sb.Append("<tr><td colspan=\"13\" class=\"meta\">infAdProd: ").Append(Esc(infAdProd.Value.Trim())).AppendLine("</td></tr>");
            }
        }
        sb.AppendLine("</tbody></table>");
        return sb.ToString();
    }

    private static string MontarTabelaItensNFCe(XElement infNFe)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th>#</th><th>Produto</th><th>NCM</th><th>CFOP</th><th class=\"num\">Qtd</th><th class=\"num\">vUn</th><th class=\"num\">vProd</th><th>Impostos</th></tr></thead><tbody>");
        var dets = infNFe.Elements().Where(e => e.Name.LocalName == "det").OrderBy(e => e.Attribute("nItem")?.Value, StringComparer.Ordinal).ToList();
        if (dets.Count == 0)
            sb.AppendLine("<tr><td colspan=\"8\">Nenhum item.</td></tr>");
        else
        {
            foreach (var det in dets)
            {
                var prod = Filho(det, "prod");
                var imposto = Filho(det, "imposto");
                sb.Append("<tr><td>").Append(Esc(det.Attribute("nItem")?.Value)).Append("</td>");
                sb.Append("<td>").Append(Esc(Texto(prod, "xProd"))).Append("</td>");
                sb.Append("<td class=\"nowrap\">").Append(Esc(Texto(prod, "NCM"))).Append("</td>");
                sb.Append("<td class=\"nowrap\">").Append(Esc(Texto(prod, "CFOP"))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Esc(Texto(prod, "qCom"))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Esc(Texto(prod, "vUnCom"))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Esc(Texto(prod, "vProd"))).Append("</td>");
                sb.Append("<td class=\"imposto-resumo\">").Append(ResumoImposto(imposto)).Append("</td></tr>");
            }
        }
        sb.AppendLine("</tbody></table>");
        return sb.ToString();
    }

    private static string ResumoImposto(XElement? imposto)
    {
        if (imposto is null) return Esc("—");
        var partes = new List<string>();
        foreach (var grupo in imposto.Elements())
        {
            foreach (var tipo in grupo.Elements())
            {
                var bits = new List<string>();
                foreach (var leaf in tipo.Elements())
                {
                    var v = leaf.Value?.Trim();
                    if (string.IsNullOrEmpty(v)) continue;
                    bits.Add($"{leaf.Name.LocalName}={v}");
                    if (bits.Count >= 14) break;
                }
                if (bits.Count > 0)
                    partes.Add($"{grupo.Name.LocalName}/{tipo.Name.LocalName}: " + string.Join(" ", bits));
            }
        }
        return partes.Count > 0 ? Esc(string.Join(" | ", partes)) : Esc("—");
    }

    private static string MontarTotaisIcms(XElement? icmsTot)
    {
        if (icmsTot is null)
            return "<p class=\"meta\">Sem grupo ICMSTot.</p>";
        return MontarTabelaGenerica(icmsTot, "ICMSTot / totais da nota");
    }

    private static string MontarTabelaGenerica(XElement bloco, string? titulo = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(titulo))
            sb.Append("<p class=\"meta\"><strong>").Append(Esc(titulo)).AppendLine("</strong></p>");
        sb.AppendLine("<table class=\"kv\">");
        foreach (var el in bloco.Elements())
        {
            if (el.HasElements) continue;
            var v = el.Value?.Trim();
            if (string.IsNullOrEmpty(v)) continue;
            sb.Append("<tr><td>").Append(Esc(el.Name.LocalName)).Append("</td><td>")
                .Append(Esc(v)).AppendLine("</td></tr>");
        }
        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static string MontarCobranca(XElement cobr)
    {
        var sb = new StringBuilder();
        var fat = Filho(cobr, "fat");
        if (fat is not null)
            sb.Append(MontarTabelaGenerica(fat, "Fatura"));

        var dups = cobr.Elements().Where(e => e.Name.LocalName == "dup").ToList();
        if (dups.Count > 0)
        {
            sb.AppendLine("<h3>Duplicatas</h3><table><thead><tr><th>nDup</th><th>dVenc</th><th class=\"num\">vDup</th></tr></thead><tbody>");
            foreach (var dup in dups)
            {
                sb.Append("<tr><td>").Append(Esc(Texto(dup, "nDup"))).Append("</td><td>")
                    .Append(Esc(Texto(dup, "dVenc"))).Append("</td><td class=\"num\">")
                    .Append(Esc(Texto(dup, "vDup"))).AppendLine("</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }
        return sb.ToString();
    }

    private static string MontarTransporte(XElement transp)
    {
        var sb = new StringBuilder();
        sb.Append(MontarTabelaKv(new List<(string, string?)>
        {
            ("Modalidade frete (modFrete)", Texto(transp, "modFrete"))
        }));

        var retTransp = Filho(transp, "retTransp");
        if (retTransp is not null)
        {
            sb.AppendLine("<h3>Retenção ICMS / serviço de transporte (retTransp)</h3>");
            sb.Append(MontarTabelaGenerica(retTransp));
        }

        var transporta = Filho(transp, "transporta");
        if (transporta is not null)
        {
            sb.AppendLine("<h3>Transportador</h3>");
            sb.Append(MontarTabelaKv(new List<(string, string?)>
            {
                ("Razão social", Texto(transporta, "xNome")),
                ("CNPJ/CPF", FormatarCnpjCpf(Texto(transporta, "CNPJ") ?? Texto(transporta, "CPF"))),
                ("IE", Texto(transporta, "IE")),
                ("Endereço", Texto(transporta, "xEnder")),
                ("Município", Texto(transporta, "xMun")),
                ("UF", Texto(transporta, "UF"))
            }));
        }

        var veic = Filho(transp, "veicTransp");
        if (veic is not null)
        {
            sb.AppendLine("<h3>Veículo</h3>");
            sb.Append(MontarTabelaGenerica(veic));
        }

        var vols = transp.Elements().Where(e => e.Name.LocalName == "vol").ToList();
        if (vols.Count > 0)
        {
            sb.AppendLine("<h3>Volumes</h3><table><thead><tr><th>qVol</th><th>esp</th><th>marca</th><th>nVol</th><th class=\"num\">pesoL</th><th class=\"num\">pesoB</th></tr></thead><tbody>");
            foreach (var vol in vols)
            {
                sb.Append("<tr><td>").Append(Esc(Texto(vol, "qVol"))).Append("</td><td>")
                    .Append(Esc(Texto(vol, "esp"))).Append("</td><td>")
                    .Append(Esc(Texto(vol, "marca"))).Append("</td><td>")
                    .Append(Esc(Texto(vol, "nVol"))).Append("</td><td class=\"num\">")
                    .Append(Esc(Texto(vol, "pesoL"))).Append("</td><td class=\"num\">")
                    .Append(Esc(Texto(vol, "pesoB"))).AppendLine("</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }
        return sb.ToString();
    }

    private static string MontarPagamento(XElement pag)
    {
        var sb = new StringBuilder();
        var detPags = pag.Elements().Where(e => e.Name.LocalName == "detPag").ToList();
        if (detPags.Count == 0)
            return MontarTabelaGenerica(pag);

        sb.AppendLine("<table><thead><tr><th>tPag</th><th class=\"num\">vPag</th><th>CNPJ cred.</th><th>Bandeira</th><th>cAut</th><th class=\"num\">vTroco</th></tr></thead><tbody>");
        foreach (var dp in detPags)
        {
            var card = Filho(dp, "card");
            sb.Append("<tr><td>").Append(Esc(Texto(dp, "tPag"))).Append("</td><td class=\"num\">")
                .Append(Esc(Texto(dp, "vPag"))).Append("</td><td>")
                .Append(Esc(Texto(card, "CNPJ"))).Append("</td><td>")
                .Append(Esc(Texto(card, "tBand"))).Append("</td><td>")
                .Append(Esc(Texto(card, "cAut"))).Append("</td><td class=\"num\">")
                .Append(Esc(Texto(dp, "vTroco"))).AppendLine("</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        return sb.ToString();
    }

    private static string MontarInfAdic(XElement? infAdic)
    {
        if (infAdic is null)
            return "<p class=\"meta\">Não há grupo infAdic.</p>";

        var sb = new StringBuilder();
        var infCpl = Texto(infAdic, "infCpl");
        if (!string.IsNullOrWhiteSpace(infCpl))
            sb.Append("<p><strong>Inf. contribuinte:</strong><br/>").Append(Esc(infCpl)).AppendLine("</p>");

        var obsCont = infAdic.Elements().Where(e => e.Name.LocalName == "obsCont").ToList();
        if (obsCont.Count > 0)
        {
            sb.AppendLine("<h3>Observações (campo / texto)</h3><table class=\"kv\"><tbody>");
            foreach (var o in obsCont)
            {
                var xCampo = o.Attribute("xCampo")?.Value;
                var xTexto = o.Attribute("xTexto")?.Value;
                if (!string.IsNullOrEmpty(xCampo) || !string.IsNullOrEmpty(xTexto))
                    sb.Append("<tr><td>").Append(Esc(xCampo)).Append("</td><td>").Append(Esc(xTexto)).AppendLine("</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        var obsFisco = infAdic.Elements().Where(e => e.Name.LocalName == "obsFisco").ToList();
        if (obsFisco.Count > 0)
        {
            sb.AppendLine("<h3>Observações fisco</h3><table class=\"kv\"><tbody>");
            foreach (var o in obsFisco)
            {
                sb.Append("<tr><td>").Append(Esc(o.Attribute("xCampo")?.Value)).Append("</td><td>")
                    .Append(Esc(o.Attribute("xTexto")?.Value)).AppendLine("</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        if (sb.Length == 0)
            sb.Append("<p class=\"meta\">infAdic sem infCpl/obsCont/obsFisco preenchidos.</p>");
        return sb.ToString();
    }

    private sealed record DanfeXmlContext(
        XElement Root,
        XElement? NFe,
        XElement? InfNFe,
        XElement? InfProt,
        XElement? InfNFeSupl);

    private static XElement? Filho(XElement? parent, string localName)
    {
        if (parent is null) return null;
        var ns = XNamespace.Get(NfeNs);
        return parent.Element(ns + localName)
               ?? parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
    }

    private static XElement? FilhoDesc(XElement parent, string localName) =>
        Filho(parent, localName) ?? parent.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);

    private static string? Texto(XElement? parent, string localName)
    {
        var el = Filho(parent, localName);
        var v = el?.Value.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static string ExtrairChaveAcesso(XElement infNFe)
    {
        var id = infNFe.Attribute("Id")?.Value;
        if (!string.IsNullOrEmpty(id) && id.StartsWith("NFe", StringComparison.OrdinalIgnoreCase) && id.Length >= 47)
            return id[3..];

        foreach (var ch in infNFe.Descendants().Where(x => x.Name.LocalName == "chNFe"))
        {
            var t = ch.Value.Trim();
            if (t.Length == 44 && t.All(char.IsDigit))
                return t;
        }

        return id ?? "";
    }

    private static string? FormatarCnpjCpf(string? digits)
    {
        if (string.IsNullOrWhiteSpace(digits)) return null;
        var d = new string(digits.Where(char.IsDigit).ToArray());
        if (d.Length == 14)
            return $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}";
        if (d.Length == 11)
            return $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}";
        return digits;
    }

    private static string? SafeHttpUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        if (!Uri.TryCreate(t, UriKind.Absolute, out var u)) return null;
        return u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps ? u.ToString() : null;
    }

    private static string Esc(string? s) => WebUtility.HtmlEncode(s ?? "");
}
