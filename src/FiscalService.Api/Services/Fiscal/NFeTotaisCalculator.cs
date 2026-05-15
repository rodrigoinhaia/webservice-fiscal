using FiscalService.Api.Models.Requests;
using NFe.Classes.Informacoes.Total;

namespace FiscalService.Api.Services.Fiscal;

/// <summary>Agrega totais da NF-e a partir dos itens informados pelo ERP.</summary>
public static class NFeTotaisCalculator
{
    public sealed record TotaisNota(
        decimal BaseIcms,
        decimal Icms,
        decimal IcmsDesonerado,
        decimal Fcp,
        decimal BaseSt,
        decimal St,
        decimal FcpSt,
        decimal FcpStRet,
        decimal Produtos,
        decimal Frete,
        decimal Seguro,
        decimal Desconto,
        decimal Ipi,
        decimal Pis,
        decimal Cofins,
        decimal Outras,
        decimal ValorNota,
        decimal FcpUfDest,
        decimal IcmsUfDest,
        decimal IcmsUfRemet);

    public static TotaisNota Calcular(IReadOnlyList<ItemNFeRequest> itens)
    {
        var produtos = itens.Sum(i => i.ValorTotalBruto);
        var desconto = itens.Sum(i => i.ValorDesconto ?? 0);
        var frete = itens.Sum(i => i.ValorFrete ?? 0);
        var seguro = itens.Sum(i => i.ValorSeguro ?? 0);
        var outras = itens.Sum(i => i.ValorOutrasDespesas ?? 0);
        var ipi = itens.Sum(i => i.ValorIpi ?? 0);
        var st = itens.Sum(i => i.ValorIcmsSt ?? 0);

        return new TotaisNota(
            BaseIcms: itens.Sum(i => i.BaseCalculoIcms ?? 0),
            Icms: itens.Sum(i => i.ValorIcms ?? 0),
            IcmsDesonerado: itens.Sum(i => i.ValorIcmsDesonerado ?? 0),
            Fcp: itens.Sum(i => i.ValorFcp ?? 0),
            BaseSt: itens.Sum(i => i.BaseCalculoIcmsSt ?? 0),
            St: st,
            FcpSt: itens.Sum(i => i.ValorFcpSt ?? 0),
            FcpStRet: itens.Sum(i => i.ValorFcpStRetido ?? 0),
            Produtos: produtos,
            Frete: frete,
            Seguro: seguro,
            Desconto: desconto,
            Ipi: ipi,
            Pis: itens.Sum(i => i.ValorPis ?? 0),
            Cofins: itens.Sum(i => i.ValorCofins ?? 0),
            Outras: outras,
            ValorNota: produtos - desconto + frete + seguro + outras + ipi + st,
            FcpUfDest: itens.Sum(i => i.ValorFcpUfDest ?? 0),
            IcmsUfDest: itens.Sum(i => i.ValorIcmsUfDest ?? 0),
            IcmsUfRemet: itens.Sum(i => i.ValorIcmsUfRemet ?? 0));
    }

    public static ICMSTot MontarIcmsTot(TotaisNota t) => new()
    {
        vBC = t.BaseIcms,
        vICMS = t.Icms,
        vICMSDeson = t.IcmsDesonerado > 0 ? t.IcmsDesonerado : 0,
        vFCP = t.Fcp > 0 ? t.Fcp : 0,
        vBCST = t.BaseSt > 0 ? t.BaseSt : 0,
        vST = t.St > 0 ? t.St : 0,
        vFCPST = t.FcpSt > 0 ? t.FcpSt : 0,
        vFCPSTRet = t.FcpStRet > 0 ? t.FcpStRet : 0,
        vProd = t.Produtos,
        vFrete = t.Frete,
        vSeg = t.Seguro,
        vDesc = t.Desconto,
        vII = 0,
        vIPI = t.Ipi,
        vIPIDevol = 0,
        vPIS = t.Pis,
        vCOFINS = t.Cofins,
        vOutro = t.Outras,
        vNF = t.ValorNota,
        vTotTrib = 0,
        vFCPUFDest = t.FcpUfDest > 0 ? t.FcpUfDest : null,
        vICMSUFDest = t.IcmsUfDest > 0 ? t.IcmsUfDest : null,
        vICMSUFRemet = t.IcmsUfRemet > 0 ? t.IcmsUfRemet : null
    };

    /// <summary>Valida coerência mínima dos totais (ERP deve enviar valores alinhados aos itens).</summary>
    public static void ValidarConsistenciaOuLancar(IReadOnlyList<ItemNFeRequest> itens, decimal tolerancia = 0.02m)
    {
        if (itens.Count == 0)
            throw new ArgumentException("A nota deve conter ao menos um item.");

        for (var i = 0; i < itens.Count; i++)
        {
            var item = itens[i];
            var bruto = item.QuantidadeComercial * item.ValorUnitarioComercial;
            if (Math.Abs(bruto - item.ValorTotalBruto) > tolerancia)
            {
                throw new TributacaoNaoSuportadaException(
                    $"Item {i + 1}: valorTotalBruto ({item.ValorTotalBruto}) diverge de quantidade × valor unitário ({bruto}).");
            }
        }

        var t = Calcular(itens);
        if (t.ValorNota < 0)
            throw new TributacaoNaoSuportadaException("Valor total da NF-e (vNF) não pode ser negativo.");
    }
}
