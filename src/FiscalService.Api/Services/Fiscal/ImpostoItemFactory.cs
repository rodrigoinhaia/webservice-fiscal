using FiscalService.Api.Models.Requests;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
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
      PIS = new PIS
      {
        TipoPIS = new PISAliq
        {
          CST = (CSTPIS)int.Parse(item.CstPis ?? "07"),
          vBC = item.BaseCalculoPis ?? 0,
          pPIS = item.AliquotaPis ?? 0,
          vPIS = item.ValorPis ?? 0
        }
      },
      COFINS = new COFINS
      {
        TipoCOFINS = new COFINSAliq
        {
          CST = (CSTCOFINS)int.Parse(item.CstCofins ?? "07"),
          vBC = item.BaseCalculoCofins ?? 0,
          pCOFINS = item.AliquotaCofins ?? 0,
          vCOFINS = item.ValorCofins ?? 0
        }
      }
    };

    var ipi = CriarIpiOpcional(item);
    if (ipi is not null)
      imp.IPI = ipi;

    return imp;
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
