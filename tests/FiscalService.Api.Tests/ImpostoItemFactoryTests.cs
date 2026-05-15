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
    public void Sem_cst_ipi_nao_inclui_grupo()
    {
        var item = new ItemNFeRequest { OrigemMercadoria = "0", CstIcms = "00" };
        var imp = ImpostoItemFactory.Criar(item, crt: 3);
        Assert.Null(imp.IPI);
    }
}
