using FiscalService.Api.Models.Requests;
using FiscalService.Api.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace FiscalService.Api.Tests;

public class NFCeEmitirRequestValidatorTests
{
    private readonly NFCeEmitirRequestValidator _validator = new(
        new ConfiguracaoEmitenteRequestValidator(),
        new ItemNFeRequestValidator());

    [Fact]
    public void NFCe_sem_csc_falha()
    {
        var model = NFCeValido();
        model.IdCsc = string.Empty;
        model.Csc = string.Empty;

        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.IdCsc);
        r.ShouldHaveValidationErrorFor(x => x.Csc);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("2", true)]
    [InlineData("3", true)]
    [InlineData("4", false)]
    [InlineData("0", false)]
    [InlineData("", true)]
    public void QrCodeVersao_aceita_apenas_1_2_ou_3(string versao, bool valido)
    {
        var model = NFCeValido();
        model.QrCodeVersao = versao;

        var r = _validator.TestValidate(model);
        if (valido) r.ShouldNotHaveValidationErrorFor(x => x.QrCodeVersao);
        else r.ShouldHaveValidationErrorFor(x => x.QrCodeVersao);
    }

    [Fact]
    public void NFCe_sem_pagamentos_falha()
    {
        var model = NFCeValido();
        model.Pagamentos = new List<PagamentoRequest>();

        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.Pagamentos);
    }

    [Fact]
    public void NFCe_completo_passa()
    {
        var model = NFCeValido();
        var r = _validator.TestValidate(model);
        r.ShouldNotHaveAnyValidationErrors();
    }

    private static NFCeEmitirRequest NFCeValido() => new()
    {
        ConfiguracaoEmitente = new ConfiguracaoEmitenteRequest
        {
            Cnpj = "12345678000190",
            RazaoSocial = "Loja Teste",
            Uf = "RS",
            Ambiente = "Homologacao",
            CertificadoPath = "c.pfx",
            CertificadoSenha = "s"
        },
        NumeroNota = 1,
        Serie = "1",
        IdCsc = "000001",
        Csc = "ABC123",
        QrCodeVersao = "2",
        Itens = new List<ItemNFeRequest>
        {
            new()
            {
                CodigoProduto = "P001",
                DescricaoProduto = "Item",
                UnidadeComercial = "UN",
                QuantidadeComercial = 1,
                ValorUnitarioComercial = 5,
                ValorTotalBruto = 5,
                Cfop = "5102"
            }
        },
        Pagamentos = new List<PagamentoRequest>
        {
            new() { FormaPagamento = "01", ValorPagamento = 5 }
        }
    };
}
