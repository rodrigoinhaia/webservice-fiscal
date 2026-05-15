using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services.Fiscal;
using Xunit;

namespace FiscalService.Api.Tests;

public class NFeTotaisCalculatorTests
{
    [Fact]
    public void Agrega_fcp_st_e_difal_nos_totais()
    {
        var itens = new List<ItemNFeRequest>
        {
            new()
            {
                QuantidadeComercial = 1,
                ValorUnitarioComercial = 100,
                ValorTotalBruto = 100,
                ValorFcp = 2,
                ValorFcpSt = 1.5m,
                ValorIcmsSt = 10,
                BaseCalculoIcmsSt = 50,
                ValorFcpUfDest = 0.5m,
                ValorIcmsUfDest = 3,
                ValorIcmsUfRemet = 7
            }
        };

        var t = NFeTotaisCalculator.Calcular(itens);
        var icms = NFeTotaisCalculator.MontarIcmsTot(t);

        Assert.Equal(2, icms.vFCP);
        Assert.Equal(1.5m, icms.vFCPST);
        Assert.Equal(10, icms.vST);
        Assert.Equal(50, icms.vBCST);
        Assert.Equal(0.5m, icms.vFCPUFDest);
        Assert.Equal(110, icms.vNF);
    }

    [Fact]
    public void Rejeita_valor_bruto_inconsistente()
    {
        var itens = new List<ItemNFeRequest>
        {
            new()
            {
                QuantidadeComercial = 2,
                ValorUnitarioComercial = 10,
                ValorTotalBruto = 999
            }
        };

        Assert.Throws<TributacaoNaoSuportadaException>(() =>
            NFeTotaisCalculator.ValidarConsistenciaOuLancar(itens));
    }
}
