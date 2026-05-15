using FiscalService.Api.Services.Fiscal;
using NFe.Classes.Informacoes.Identificacao.Tipos;
using Xunit;

namespace FiscalService.Api.Tests;

public class ContingenciaEmissaoMapperTests
{
    [Theory]
    [InlineData("SVC-AN", nameof(TipoEmissao.teSVCAN))]
    [InlineData("svc-rs", nameof(TipoEmissao.teSVCRS))]
    [InlineData(null, nameof(TipoEmissao.teNormal))]
    public void Resolve_tipo_emissao(string? entrada, string esperado)
    {
        var valor = ContingenciaEmissaoMapper.Resolver(entrada);
        Assert.Equal(esperado, Enum.GetName(typeof(TipoEmissao), valor));
    }

    [Fact]
    public void Tipo_invalido_lanca()
    {
        Assert.Throws<ArgumentException>(() => ContingenciaEmissaoMapper.Resolver("INVALIDO"));
    }
}
