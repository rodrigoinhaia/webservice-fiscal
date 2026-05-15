using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services.Fiscal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal;
using Xunit;

namespace FiscalService.Api.Tests;

public class ImpostoItemFactoryTests
{
    [Fact]
    public void Monta_ipi_tributado_quando_cst_00()
    {
        var item = new ItemNFeRequest
        {
            OrigemMercadoria = "0",
            CstIpi = "00",
            ValorIpi = 1.5m,
            AliquotaIpi = 5m,
            BaseCalculoIpi = 30m,
            ValorTotalBruto = 30m
        };

        var imp = ImpostoItemFactory.Criar(item, crt: 3);
        Assert.NotNull(imp.IPI);
        Assert.IsType<IPITrib>(imp.IPI!.TipoIPI);
    }

    [Fact]
    public void Monta_ipi_nt_quando_cst_53()
    {
        var item = new ItemNFeRequest
        {
            OrigemMercadoria = "0",
            CstIpi = "53"
        };

        var imp = ImpostoItemFactory.Criar(item, crt: 3);
        Assert.NotNull(imp.IPI);
        Assert.IsType<IPINT>(imp.IPI!.TipoIPI);
    }

    [Fact]
    public void Monta_icms_uf_dest_quando_difal_informado()
    {
        var item = new ItemNFeRequest
        {
            OrigemMercadoria = "0",
            CstIcms = "00",
            BaseCalculoUfDest = 100,
            PercentualIcmsUfDest = 18,
            PercentualIcmsInter = 12,
            PercentualIcmsInterPartilha = 40,
            ValorIcmsUfDest = 5,
            ValorIcmsUfRemet = 7
        };

        var imp = ImpostoItemFactory.Criar(item, crt: 3);
        Assert.NotNull(imp.ICMSUFDest);
        Assert.Equal(100, imp.ICMSUFDest!.vBCUFDest);
    }

    [Fact]
    public void Monta_pis_nt_cst_07()
    {
        var item = new ItemNFeRequest
        {
            OrigemMercadoria = "0",
            CstPis = "07",
            CstCofins = "07",
            CstIcms = "00"
        };

        var imp = ImpostoItemFactory.Criar(item, crt: 3);
        Assert.IsType<PISNT>(imp.PIS!.TipoPIS);
        Assert.IsType<COFINSNT>(imp.COFINS!.TipoCOFINS);
    }

    [Fact]
    public void Monta_pis_cofins_cst_03_por_quantidade()
    {
        var item = new ItemNFeRequest
        {
            OrigemMercadoria = "0",
            CstIcms = "00",
            CstPis = "03",
            CstCofins = "03",
            QuantidadeComercial = 10,
            ValorUnitarioComercial = 2.5m,
            ValorPis = 5m,
            ValorCofins = 7m
        };

        var imp = ImpostoItemFactory.Criar(item, crt: 3);
        Assert.IsType<PISQtde>(imp.PIS!.TipoPIS);
        Assert.IsType<COFINSQtde>(imp.COFINS!.TipoCOFINS);
        var pis = (PISQtde)imp.PIS.TipoPIS;
        Assert.Equal(10, pis.qBCProd);
        Assert.Equal(5m, pis.vPIS);
    }

    [Fact]
    public void Sem_cst_ipi_nao_inclui_grupo()
    {
        var item = new ItemNFeRequest { OrigemMercadoria = "0", CstIcms = "00" };
        var imp = ImpostoItemFactory.Criar(item, crt: 3);
        Assert.Null(imp.IPI);
    }
}
