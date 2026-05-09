using FiscalService.Api.Models.Requests;
using FiscalService.Api.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace FiscalService.Api.Tests;

public class ItemNFeRequestValidatorTests
{
    private readonly ItemNFeRequestValidator _validator = new();

    [Fact]
    public void Item_minimo_valido_passa()
    {
        var model = new ItemNFeRequest
        {
            CodigoProduto = "001",
            DescricaoProduto = "Produto Teste",
            UnidadeComercial = "UN",
            QuantidadeComercial = 1,
            ValorUnitarioComercial = 10,
            ValorTotalBruto = 10,
            Cfop = "5102"
        };

        var r = _validator.TestValidate(model);
        r.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Quantidade_zero_falha()
    {
        var model = new ItemNFeRequest
        {
            CodigoProduto = "001",
            DescricaoProduto = "X",
            UnidadeComercial = "UN",
            QuantidadeComercial = 0,
            ValorUnitarioComercial = 10,
            ValorTotalBruto = 0
        };

        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.QuantidadeComercial);
    }

    [Theory]
    [InlineData("510")]
    [InlineData("51022")]
    [InlineData("510a")]
    public void Cfop_invalido_falha(string cfop)
    {
        var model = ItemValido(cfop: cfop);
        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.Cfop);
    }

    [Theory]
    [InlineData("5102")]
    [InlineData("6102")]
    [InlineData(null)]
    public void Cfop_valido_ou_ausente_passa(string? cfop)
    {
        var model = ItemValido(cfop: cfop);
        var r = _validator.TestValidate(model);
        r.ShouldNotHaveValidationErrorFor(x => x.Cfop);
    }

    [Theory]
    [InlineData("00", true)]
    [InlineData("60", true)]
    [InlineData("0", true)]
    [InlineData("a1", false)]
    [InlineData("123", false)]
    public void Cst_icms_aceita_um_ou_dois_digitos_numericos(string cst, bool valido)
    {
        var model = ItemValido();
        model.CstIcms = cst;
        var r = _validator.TestValidate(model);
        if (valido) r.ShouldNotHaveValidationErrorFor(x => x.CstIcms);
        else r.ShouldHaveValidationErrorFor(x => x.CstIcms);
    }

    [Theory]
    [InlineData("102", true)]
    [InlineData("900", true)]
    [InlineData("12", false)]
    [InlineData("ABC", false)]
    public void Csosn_exige_3_digitos_numericos(string csosn, bool valido)
    {
        var model = ItemValido();
        model.CsosnIcms = csosn;
        var r = _validator.TestValidate(model);
        if (valido) r.ShouldNotHaveValidationErrorFor(x => x.CsosnIcms);
        else r.ShouldHaveValidationErrorFor(x => x.CsosnIcms);
    }

    private static ItemNFeRequest ItemValido(string? cfop = "5102") => new()
    {
        CodigoProduto = "001",
        DescricaoProduto = "Produto",
        UnidadeComercial = "UN",
        QuantidadeComercial = 1,
        ValorUnitarioComercial = 10,
        ValorTotalBruto = 10,
        Cfop = cfop
    };
}
