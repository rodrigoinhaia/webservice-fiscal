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

        return imp;
    }
}
