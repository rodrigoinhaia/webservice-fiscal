using FiscalService.Api.Helpers;
using Xunit;

namespace FiscalService.Api.Tests;

public class UfHelperTests
{
    [Theory]
    [InlineData("RS")]
    [InlineData("rs")]
    [InlineData(" sp ")]
    [InlineData("AC")]
    [InlineData("DF")]
    [InlineData("MG")]
    [InlineData("TO")]
    public void Mapear_aceita_27_estados_e_normaliza(string uf)
    {
        var estado = UfHelper.MapearUf(uf);
        Assert.True((int)estado > 0);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("BR")]
    [InlineData("")]
    [InlineData("BRASIL")]
    public void Mapear_invalida_lanca_argument(string uf)
    {
        Assert.Throws<ArgumentException>(() => UfHelper.MapearUf(uf));
    }

    [Theory]
    [InlineData("RS", true)]
    [InlineData("XX", false)]
    [InlineData("", false)]
    public void IsValid_classifica_corretamente(string uf, bool esperado) =>
        Assert.Equal(esperado, UfHelper.IsValid(uf));
}
