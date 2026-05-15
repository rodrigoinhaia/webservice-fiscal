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
    public void Sem_cst_ipi_nao_inclui_grupo()
    {
        var item = new ItemNFeRequest { OrigemMercadoria = "0", CstIcms = "00" };
        var imp = ImpostoItemFactory.Criar(item, crt: 3);
        Assert.Null(imp.IPI);
    }
}
