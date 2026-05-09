using FiscalService.Api.Models.Requests;
using FiscalService.Api.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace FiscalService.Api.Tests;

public class CTeEmitirRequestValidatorTests
{
    private readonly CTeEmitirRequestValidator _validator = new(new ConfiguracaoEmitenteRequestValidator());

    [Theory]
    [InlineData("1", true)]
    [InlineData("01", true)]
    [InlineData("06", true)]
    [InlineData("0", false)]
    [InlineData("7", false)]
    [InlineData("01a", false)]
    public void Modal_aceita_01_a_06(string modal, bool valido)
    {
        var model = CTeValido();
        model.Modal = modal;
        var r = _validator.TestValidate(model);

        if (valido) r.ShouldNotHaveValidationErrorFor(x => x.Modal);
        else r.ShouldHaveValidationErrorFor(x => x.Modal);
    }

    [Theory]
    [InlineData("6351", true)]
    [InlineData("635", false)]
    [InlineData("63510", false)]
    [InlineData("ABCD", false)]
    public void Cfop_4_digitos_numericos(string cfop, bool valido)
    {
        var model = CTeValido();
        model.Cfop = cfop;
        var r = _validator.TestValidate(model);

        if (valido) r.ShouldNotHaveValidationErrorFor(x => x.Cfop);
        else r.ShouldHaveValidationErrorFor(x => x.Cfop);
    }

    [Fact]
    public void CTe_sem_remetente_falha()
    {
        var model = CTeValido();
        model.Remetente = null!;
        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.Remetente);
    }

    private static CTeEmitirRequest CTeValido() => new()
    {
        ConfiguracaoEmitente = new ConfiguracaoEmitenteRequest
        {
            Cnpj = "12345678000190",
            RazaoSocial = "Transp",
            Uf = "RS",
            Ambiente = "Homologacao",
            CertificadoPath = "c.pfx",
            CertificadoSenha = "s"
        },
        NumeroNota = 1,
        Serie = "1",
        Cfop = "6351",
        Modal = "01",
        Remetente = new RemetenteCTeRequest { RazaoSocial = "R" },
        Destinatario = new DestinatarioRequest { RazaoSocial = "D", IndicadorIe = 9 },
        Tomador = new TomadorCTeRequest(),
        ValorTotalServico = 100m
    };
}
