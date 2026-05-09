using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services.Fiscal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using Xunit;

namespace FiscalService.Api.Tests;

public class ImpostoIcmsMapperTests
{
    [Theory]
    [InlineData("00", typeof(ICMS00))]
    [InlineData("0", typeof(ICMS00))]
    [InlineData("40", typeof(ICMS40))]
    [InlineData("41", typeof(ICMS40))]
    [InlineData("50", typeof(ICMS40))]
    [InlineData("60", typeof(ICMS60))]
    public void Regime_normal_mapeia_cst_para_grupo_correto(string cst, Type tipoEsperado)
    {
        var item = new ItemNFeRequest
        {
            CstIcms = cst,
            BaseCalculoIcms = 100,
            AliquotaIcms = 18,
            ValorIcms = 18,
            OrigemMercadoria = "0"
        };

        var icms = ImpostoIcmsMapper.CriarIcms(item, crt: 3);
        Assert.IsType(tipoEsperado, icms.TipoICMS);
    }

    [Theory]
    [InlineData("101", typeof(ICMSSN101))]
    [InlineData("102", typeof(ICMSSN102))]
    [InlineData("103", typeof(ICMSSN201))]
    [InlineData("201", typeof(ICMSSN201))]
    [InlineData("202", typeof(ICMSSN202))]
    [InlineData("203", typeof(ICMSSN202))]
    [InlineData("500", typeof(ICMSSN500))]
    [InlineData("900", typeof(ICMSSN900))]
    public void Simples_nacional_mapeia_csosn_para_grupo_correto(string csosn, Type tipoEsperado)
    {
        var item = new ItemNFeRequest
        {
            CsosnIcms = csosn,
            BaseCalculoIcms = 100,
            AliquotaIcms = 18,
            ValorIcms = 18,
            OrigemMercadoria = "0"
        };

        var icms = ImpostoIcmsMapper.CriarIcms(item, crt: 1);
        Assert.IsType(tipoEsperado, icms.TipoICMS);
    }

    [Fact]
    public void CRT_simples_sem_csosn_usa_default_102()
    {
        var item = new ItemNFeRequest { OrigemMercadoria = "0" };
        var icms = ImpostoIcmsMapper.CriarIcms(item, crt: 1);
        Assert.IsType<ICMSSN102>(icms.TipoICMS);
    }

    [Fact]
    public void CRT_normal_sem_cst_usa_default_00()
    {
        var item = new ItemNFeRequest { OrigemMercadoria = "0" };
        var icms = ImpostoIcmsMapper.CriarIcms(item, crt: 3);
        Assert.IsType<ICMS00>(icms.TipoICMS);
    }
}
