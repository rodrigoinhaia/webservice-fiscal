using FiscalService.Api.Models.Requests;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;

namespace FiscalService.Api.Services.Fiscal;

/// <summary>
/// Monta o grupo <c>ICMS</c> do item conforme CRT (1/2 Simples, 3 Normal), <see cref="ItemNFeRequest.CstIcms"/> ou <see cref="ItemNFeRequest.CsosnIcms"/>.
/// CST com 2 dígitos; CSOSN com 3 dígitos. Combinações não suportadas usam ICMS00 (CRT 3) ou ICMSSN102 (CRT 1/2).
/// </summary>
public static class ImpostoIcmsMapper
{
    private static readonly DeterminacaoBaseIcmsSt ModBcStPadrao = (DeterminacaoBaseIcmsSt)4;

    public static ICMS CriarIcms(ItemNFeRequest item, int crt)
    {
        var origem = (OrigemMercadoria)int.Parse(item.OrigemMercadoria ?? "0");
        return crt is 1 or 2 ? CriarSimples(item, origem) : CriarRegimeNormal(item, origem);
    }

    private static ICMS CriarRegimeNormal(ItemNFeRequest item, OrigemMercadoria origem)
    {
        var cst = NormalizarCst(item.CstIcms) ?? "00";
        return cst switch
        {
            "40" or "41" or "50" => Icms40(item, origem, cst),
            "60" => Icms60(item, origem),
            _ => Icms00(item, origem)
        };
    }

    private static ICMS CriarSimples(ItemNFeRequest item, OrigemMercadoria origem)
    {
        var cs = NormalizarCsosn(item.CsosnIcms) ?? "102";
        return cs switch
        {
            "101" => Sn101(item, origem),
            "103" => Sn103(item, origem),
            "201" => Sn201(item, origem),
            "202" or "203" => Sn202(item, origem, cs),
            "500" => Sn500(item, origem),
            "900" => Sn900(item, origem),
            _ => Sn102(item, origem)
        };
    }

    private static string? NormalizarCst(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var d = s.Trim();
        if (d.Length == 1 && char.IsDigit(d[0])) return "0" + d;
        return d.Length >= 2 ? d[..2] : d;
    }

    private static string? NormalizarCsosn(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var d = s.Trim();
        if (d.Length == 1 && char.IsDigit(d[0])) return d.PadLeft(3, '0');
        if (d.Length == 2 && d.All(char.IsDigit)) return d.PadLeft(3, '0');
        return d.Length >= 3 ? d[..3] : d;
    }

    private static ICMS Icms00(ItemNFeRequest item, OrigemMercadoria origem) => new()
    {
        TipoICMS = new ICMS00
        {
            orig = origem,
            CST = Csticms.Cst00,
            modBC = DeterminacaoBaseIcms.DbiValorOperacao,
            vBC = item.BaseCalculoIcms ?? 0,
            pICMS = item.AliquotaIcms ?? 0,
            vICMS = item.ValorIcms ?? 0
        }
    };

    private static ICMS Icms40(ItemNFeRequest item, OrigemMercadoria origem, string cst) => new()
    {
        TipoICMS = new ICMS40
        {
            orig = origem,
            CST = cst switch
            {
                "41" => Csticms.Cst41,
                "50" => Csticms.Cst50,
                _ => Csticms.Cst40
            },
            vICMSDeson = item.ValorIcmsDesonerado,
            motDesICMS = item.MotivoDesoneracaoIcms is { } m ? (MotivoDesoneracaoIcms)m : null
        }
    };

    private static ICMS Icms60(ItemNFeRequest item, OrigemMercadoria origem) => new()
    {
        TipoICMS = new ICMS60
        {
            orig = origem,
            CST = Csticms.Cst60,
            vBCSTRet = item.BaseCalculoIcmsStRetido ?? 0,
            pST = item.AliquotaSuportadaConsumidorFinal ?? 0,
            vICMSSubstituto = item.ValorIcmsSubstituto,
            vICMSSTRet = item.ValorIcmsStRetido ?? 0,
            vBCFCPSTRet = item.BaseCalculoFcpStRetido,
            pFCPSTRet = item.AliquotaFcpStRetido,
            vFCPSTRet = item.ValorFcpStRetido,
            pRedBCEfet = item.PercentualReducaoBcEfetivo,
            vBCEfet = item.BaseCalculoEfetivo,
            pICMSEfet = item.AliquotaIcmsEfetivo,
            vICMSEfet = item.ValorIcmsEfetivo
        }
    };

    private static ICMS Sn101(ItemNFeRequest item, OrigemMercadoria origem) => new()
    {
        TipoICMS = new ICMSSN101
        {
            orig = origem,
            CSOSN = Csosnicms.Csosn101,
            pCredSN = item.AliquotaCreditoSimples ?? 0,
            vCredICMSSN = item.ValorCreditoSimples ?? 0
        }
    };

    private static ICMS Sn102(ItemNFeRequest item, OrigemMercadoria origem) => new()
    {
        TipoICMS = new ICMSSN102 { orig = origem, CSOSN = Csosnicms.Csosn102 }
    };

    /// <summary>CSOSN 103 — no layout Zeus usa o grupo ICMSSN201 com CSOSN 103.</summary>
    private static ICMS Sn103(ItemNFeRequest item, OrigemMercadoria origem) => new()
    {
        TipoICMS = new ICMSSN201
        {
            orig = origem,
            CSOSN = Csosnicms.Csosn103,
            modBCST = ModBcStPadrao,
            pMVAST = item.PercentualMvaSt ?? 0,
            pRedBCST = item.PercentualReducaoBcSt ?? 0,
            vBCST = item.BaseCalculoIcmsSt ?? 0,
            pICMSST = item.AliquotaIcmsSt ?? 0,
            vICMSST = item.ValorIcmsSt ?? 0,
            pCredSN = item.AliquotaCreditoSimples ?? 0,
            vCredICMSSN = item.ValorCreditoSimples ?? 0
        }
    };

    private static ICMS Sn201(ItemNFeRequest item, OrigemMercadoria origem) => new()
    {
        TipoICMS = new ICMSSN201
        {
            orig = origem,
            CSOSN = Csosnicms.Csosn201,
            modBCST = ModBcStPadrao,
            pMVAST = item.PercentualMvaSt ?? 0,
            pRedBCST = item.PercentualReducaoBcSt ?? 0,
            vBCST = item.BaseCalculoIcmsSt ?? 0,
            pICMSST = item.AliquotaIcmsSt ?? 0,
            vICMSST = item.ValorIcmsSt ?? 0,
            pCredSN = item.AliquotaCreditoSimples ?? 0,
            vCredICMSSN = item.ValorCreditoSimples ?? 0
        }
    };

    private static ICMS Sn202(ItemNFeRequest item, OrigemMercadoria origem, string cs) => new()
    {
        TipoICMS = new ICMSSN202
        {
            orig = origem,
            CSOSN = cs == "203" ? Csosnicms.Csosn203 : Csosnicms.Csosn202,
            modBCST = ModBcStPadrao,
            pMVAST = item.PercentualMvaSt ?? 0,
            pRedBCST = item.PercentualReducaoBcSt ?? 0,
            vBCST = item.BaseCalculoIcmsSt ?? 0,
            pICMSST = item.AliquotaIcmsSt ?? 0,
            vICMSST = item.ValorIcmsSt ?? 0
        }
    };

    private static ICMS Sn500(ItemNFeRequest item, OrigemMercadoria origem) => new()
    {
        TipoICMS = new ICMSSN500
        {
            orig = origem,
            CSOSN = Csosnicms.Csosn500,
            vBCSTRet = item.BaseCalculoIcmsStRetido ?? 0,
            pST = item.AliquotaSuportadaConsumidorFinal ?? 0,
            vICMSSTRet = item.ValorIcmsStRetido ?? 0
        }
    };

    private static ICMS Sn900(ItemNFeRequest item, OrigemMercadoria origem) => new()
    {
        TipoICMS = new ICMSSN900
        {
            orig = origem,
            CSOSN = Csosnicms.Csosn900,
            modBC = DeterminacaoBaseIcms.DbiValorOperacao,
            vBC = item.BaseCalculoIcms ?? 0,
            pRedBC = item.ReducaoBaseIcms,
            pICMS = item.AliquotaIcms,
            vICMS = item.ValorIcms,
            modBCST = ModBcStPadrao,
            pMVAST = item.PercentualMvaSt,
            pRedBCST = item.PercentualReducaoBcSt,
            vBCST = item.BaseCalculoIcmsSt,
            pICMSST = item.AliquotaIcmsSt,
            vICMSST = item.ValorIcmsSt,
            pCredSN = item.AliquotaCreditoSimples,
            vCredICMSSN = item.ValorCreditoSimples
        }
    };
}
