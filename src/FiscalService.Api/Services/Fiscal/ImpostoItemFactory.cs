using FiscalService.Api.Models.Requests;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal.Tipos;

namespace FiscalService.Api.Services.Fiscal;

public static class ImpostoItemFactory
{
  public static imposto Criar(ItemNFeRequest item, int crt)
  {
    var imp = new imposto
    {
      ICMS = ImpostoIcmsMapper.CriarIcms(item, crt),
      PIS = CriarPis(item),
      COFINS = CriarCofins(item)
    };

    var ipi = CriarIpiOpcional(item);
    if (ipi is not null)
      imp.IPI = ipi;

    var difal = CriarDifalOpcional(item);
    if (difal is not null)
      imp.ICMSUFDest = difal;

    return imp;
  }

  private static PIS CriarPis(ItemNFeRequest item)
  {
    var cst = ImpostoTributacaoCatalog.NormalizarCst(item.CstPis) ?? "07";

    if (ImpostoTributacaoCatalog.CstPisCofinsAliquota.Contains(cst))
    {
      return new PIS
      {
        TipoPIS = new PISAliq
        {
          CST = (CSTPIS)int.Parse(cst),
          vBC = item.BaseCalculoPis ?? 0,
          pPIS = item.AliquotaPis ?? 0,
          vPIS = item.ValorPis ?? 0
        }
      };
    }

    if (ImpostoTributacaoCatalog.CstPisCofinsNaoTributado.Contains(cst))
    {
      return new PIS
      {
        TipoPIS = new PISNT { CST = (CSTPIS)int.Parse(cst) }
      };
    }

    if (ImpostoTributacaoCatalog.CstPisCofinsOutros.Contains(cst))
    {
      return new PIS
      {
        TipoPIS = new PISOutr
        {
          CST = (CSTPIS)int.Parse(cst),
          vBC = item.BaseCalculoPis,
          pPIS = item.AliquotaPis,
          qBCProd = item.QuantidadeTributavel,
          vAliqProd = item.ValorUnitarioTributavel,
          vPIS = item.ValorPis ?? 0
        }
      };
    }

    throw new TributacaoNaoSuportadaException($"CST PIS '{cst}' não suportado.");
  }

  private static COFINS CriarCofins(ItemNFeRequest item)
  {
    var cst = ImpostoTributacaoCatalog.NormalizarCst(item.CstCofins) ?? "07";

    if (ImpostoTributacaoCatalog.CstPisCofinsAliquota.Contains(cst))
    {
      return new COFINS
      {
        TipoCOFINS = new COFINSAliq
        {
          CST = (CSTCOFINS)int.Parse(cst),
          vBC = item.BaseCalculoCofins ?? 0,
          pCOFINS = item.AliquotaCofins ?? 0,
          vCOFINS = item.ValorCofins ?? 0
        }
      };
    }

    if (ImpostoTributacaoCatalog.CstPisCofinsNaoTributado.Contains(cst))
    {
      return new COFINS
      {
        TipoCOFINS = new COFINSNT { CST = (CSTCOFINS)int.Parse(cst) }
      };
    }

    if (ImpostoTributacaoCatalog.CstPisCofinsOutros.Contains(cst))
    {
      return new COFINS
      {
        TipoCOFINS = new COFINSOutr
        {
          CST = (CSTCOFINS)int.Parse(cst),
          vBC = item.BaseCalculoCofins,
          pCOFINS = item.AliquotaCofins,
          qBCProd = item.QuantidadeTributavel,
          vAliqProd = item.ValorUnitarioTributavel,
          vCOFINS = item.ValorCofins ?? 0
        }
      };
    }

    throw new TributacaoNaoSuportadaException($"CST COFINS '{cst}' não suportado.");
  }

  private static ICMSUFDest? CriarDifalOpcional(ItemNFeRequest item)
  {
    if (item.BaseCalculoUfDest is not { } vBc) return null;

    return new ICMSUFDest
    {
      vBCUFDest = vBc,
      pFCPUFDest = item.PercentualFcpUfDest,
      pICMSUFDest = item.PercentualIcmsUfDest ?? 0,
      pICMSInter = item.PercentualIcmsInter ?? 0,
      pICMSInterPart = item.PercentualIcmsInterPartilha ?? 0,
      vFCPUFDest = item.ValorFcpUfDest,
      vICMSUFDest = item.ValorIcmsUfDest ?? 0,
      vICMSUFRemet = item.ValorIcmsUfRemet ?? 0
    };
  }

  private static IPI? CriarIpiOpcional(ItemNFeRequest item)
  {
    var cst = ImpostoTributacaoCatalog.NormalizarCstIpi(item.CstIpi);
    if (cst is null && (item.ValorIpi ?? 0) == 0)
      return null;

    cst ??= "99";

    if (ImpostoTributacaoCatalog.CstIpiNaoTributado.Contains(cst))
    {
      return new IPI
      {
        TipoIPI = new IPINT { CST = (CSTIPI)Enum.Parse(typeof(CSTIPI), "ipi" + cst, ignoreCase: true) }
      };
    }

    return new IPI
    {
      TipoIPI = new IPITrib
      {
        CST = (CSTIPI)Enum.Parse(typeof(CSTIPI), "ipi" + cst, ignoreCase: true),
        vBC = item.BaseCalculoIpi ?? item.ValorTotalBruto,
        pIPI = item.AliquotaIpi,
        vIPI = item.ValorIpi ?? 0
      }
    };
  }
}
