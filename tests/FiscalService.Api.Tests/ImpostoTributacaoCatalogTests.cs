using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services.Fiscal;
using Xunit;

namespace FiscalService.Api.Tests;

public class ImpostoTributacaoCatalogTests
{
    [Fact]
    public void CRT3_rejeita_csosn_no_item()
    {
        var item = new ItemNFeRequest { CsosnIcms = "102", OrigemMercadoria = "0" };
        Assert.False(ImpostoTributacaoCatalog.ValidarItem(item, 3, out var msg));
        Assert.Contains("csosnIcms", msg!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CRT1_rejeita_cst_no_item()
    {
        var item = new ItemNFeRequest { CstIcms = "00", OrigemMercadoria = "0" };
        Assert.False(ImpostoTributacaoCatalog.ValidarItem(item, 1, out var msg));
        Assert.Contains("cstIcms", msg!, StringComparison.OrdinalIgnoreCase);
    }
}
