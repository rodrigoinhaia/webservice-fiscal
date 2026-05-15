using FiscalService.Api.Models.Requests;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;

namespace FiscalService.Api.Services.Fiscal;

/// <summary>
/// Monta o grupo <c>ICMS</c> conforme CRT e CST/CSOSN (classes DFe.NET / ZeusAutomacao).
/// </summary>
public static class ImpostoIcmsMapper
{
  private static readonly DeterminacaoBaseIcmsSt ModBcStPadrao = (DeterminacaoBaseIcmsSt)4;

  public static ICMS CriarIcms(ItemNFeRequest item, int crt)
  {
    if (!ImpostoTributacaoCatalog.ValidarItem(item, crt, out var msg))
      throw new TributacaoNaoSuportadaException(msg!);

    var origem = (OrigemMercadoria)int.Parse(item.OrigemMercadoria ?? "0");
    return crt is 1 or 2 ? CriarSimples(item, origem) : CriarRegimeNormal(item, origem);
  }

  private static ICMS CriarRegimeNormal(ItemNFeRequest item, OrigemMercadoria origem)
  {
    return ImpostoTributacaoCatalog.CstEfetivoRegimeNormal(item) switch
    {
      "10" => Icms10(item, origem),
      "20" => Icms20(item, origem),
      "30" => Icms30(item, origem),
      "40" or "41" or "50" => Icms40(item, origem, ImpostoTributacaoCatalog.CstEfetivoRegimeNormal(item)),
      "51" => Icms51(item, origem),
      "60" => Icms60(item, origem),
      "70" => Icms70(item, origem),
      "90" => Icms90(item, origem),
      _ => Icms00(item, origem)
    };
  }

  private static ICMS CriarSimples(ItemNFeRequest item, OrigemMercadoria origem)
  {
    return ImpostoTributacaoCatalog.CsosnEfetivoSimples(item) switch
    {
      "101" => Sn101(item, origem),
      "103" => Sn103(item, origem),
      "201" => Sn201(item, origem),
      "202" or "203" => Sn202(item, origem, ImpostoTributacaoCatalog.CsosnEfetivoSimples(item)),
      "500" => Sn500(item, origem),
      "900" => Sn900(item, origem),
      _ => Sn102(item, origem)
    };
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

  private static ICMS Icms10(ItemNFeRequest item, OrigemMercadoria origem) => new()
  {
    TipoICMS = new ICMS10
    {
      orig = origem,
      CST = Csticms.Cst10,
      modBC = DeterminacaoBaseIcms.DbiValorOperacao,
      vBC = item.BaseCalculoIcms ?? 0,
      pICMS = item.AliquotaIcms ?? 0,
      vICMS = item.ValorIcms ?? 0,
      modBCST = ModBcStPadrao,
      pMVAST = item.PercentualMvaSt,
      pRedBCST = item.PercentualReducaoBcSt,
      vBCST = item.BaseCalculoIcmsSt ?? 0,
      pICMSST = item.AliquotaIcmsSt ?? 0,
      vICMSST = item.ValorIcmsSt ?? 0
    }
  };

  private static ICMS Icms20(ItemNFeRequest item, OrigemMercadoria origem) => new()
  {
    TipoICMS = new ICMS20
    {
      orig = origem,
      CST = Csticms.Cst20,
      modBC = DeterminacaoBaseIcms.DbiValorOperacao,
      pRedBC = item.ReducaoBaseIcms ?? 0,
      vBC = item.BaseCalculoIcms ?? 0,
      pICMS = item.AliquotaIcms ?? 0,
      vICMS = item.ValorIcms ?? 0,
      vICMSDeson = item.ValorIcmsDesonerado,
      motDesICMS = item.MotivoDesoneracaoIcms is { } m ? (MotivoDesoneracaoIcms)m : null
    }
  };

  private static ICMS Icms30(ItemNFeRequest item, OrigemMercadoria origem) => new()
  {
    TipoICMS = new ICMS30
    {
      orig = origem,
      CST = Csticms.Cst30,
      modBCST = ModBcStPadrao,
      pMVAST = item.PercentualMvaSt,
      pRedBCST = item.PercentualReducaoBcSt,
      vBCST = item.BaseCalculoIcmsSt ?? 0,
      pICMSST = item.AliquotaIcmsSt ?? 0,
      vICMSST = item.ValorIcmsSt ?? 0,
      vICMSDeson = item.ValorIcmsDesonerado,
      motDesICMS = item.MotivoDesoneracaoIcms is { } m ? (MotivoDesoneracaoIcms)m : null
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

  private static ICMS Icms51(ItemNFeRequest item, OrigemMercadoria origem) => new()
  {
    TipoICMS = new ICMS51
    {
      orig = origem,
      CST = Csticms.Cst51,
      modBC = DeterminacaoBaseIcms.DbiValorOperacao,
      pRedBC = item.ReducaoBaseIcms,
      vBC = item.BaseCalculoIcms,
      pICMS = item.AliquotaIcms,
      vICMSOp = item.ValorIcmsOperacao,
      pDif = item.PercentualDiferimentoIcms,
      vICMSDif = item.ValorIcmsDiferido,
      vICMS = item.ValorIcms
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

  private static ICMS Icms70(ItemNFeRequest item, OrigemMercadoria origem) => new()
  {
    TipoICMS = new ICMS70
    {
      orig = origem,
      CST = Csticms.Cst70,
      modBC = DeterminacaoBaseIcms.DbiValorOperacao,
      pRedBC = item.ReducaoBaseIcms ?? 0,
      vBC = item.BaseCalculoIcms ?? 0,
      pICMS = item.AliquotaIcms ?? 0,
      vICMS = item.ValorIcms ?? 0,
      modBCST = ModBcStPadrao,
      pMVAST = item.PercentualMvaSt,
      pRedBCST = item.PercentualReducaoBcSt,
      vBCST = item.BaseCalculoIcmsSt ?? 0,
      pICMSST = item.AliquotaIcmsSt ?? 0,
      vICMSST = item.ValorIcmsSt ?? 0,
      vICMSDeson = item.ValorIcmsDesonerado,
      motDesICMS = item.MotivoDesoneracaoIcms is { } m ? (MotivoDesoneracaoIcms)m : null
    }
  };

  private static ICMS Icms90(ItemNFeRequest item, OrigemMercadoria origem) => new()
  {
    TipoICMS = new ICMS90
    {
      orig = origem,
      CST = Csticms.Cst90,
      modBC = DeterminacaoBaseIcms.DbiValorOperacao,
      vBC = item.BaseCalculoIcms,
      pRedBC = item.ReducaoBaseIcms,
      pICMS = item.AliquotaIcms,
      vICMS = item.ValorIcms,
      modBCST = ModBcStPadrao,
      pMVAST = item.PercentualMvaSt,
      pRedBCST = item.PercentualReducaoBcSt,
      vBCST = item.BaseCalculoIcmsSt,
      pICMSST = item.AliquotaIcmsSt,
      vICMSST = item.ValorIcmsSt,
      vICMSDeson = item.ValorIcmsDesonerado,
      motDesICMS = item.MotivoDesoneracaoIcms is { } m ? (MotivoDesoneracaoIcms)m : null
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
