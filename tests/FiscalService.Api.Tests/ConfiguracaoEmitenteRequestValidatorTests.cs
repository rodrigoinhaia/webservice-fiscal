using FiscalService.Api.Models.Requests;
using FiscalService.Api.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace FiscalService.Api.Tests;

public class ConfiguracaoEmitenteRequestValidatorTests
{
    private readonly ConfiguracaoEmitenteRequestValidator _validator = new();

    [Fact]
    public void Cnpj_com_menos_de_14_digitos_falha()
    {
        var model = new ConfiguracaoEmitenteRequest
        {
            Cnpj = "123",
            RazaoSocial = "X",
            Uf = "RS",
            Ambiente = "Homologacao",
            CertificadoPath = "a.pfx",
            CertificadoSenha = "x"
        };

        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.Cnpj);
    }

    [Fact]
    public void Cnpj_formatado_14_digitos_passa()
    {
        var model = new ConfiguracaoEmitenteRequest
        {
            Cnpj = "12.345.678/0001-90",
            RazaoSocial = "Empresa Teste",
            Uf = "RS",
            Ambiente = "Homologacao",
            CertificadoPath = "a.pfx",
            CertificadoSenha = "x"
        };

        var r = _validator.TestValidate(model);
        r.ShouldNotHaveValidationErrorFor(x => x.Cnpj);
    }

    [Fact]
    public void Ambiente_invalido_falha()
    {
        var model = new ConfiguracaoEmitenteRequest
        {
            Cnpj = "12345678000190",
            RazaoSocial = "X",
            Uf = "RS",
            Ambiente = "Dev",
            CertificadoPath = "a.pfx",
            CertificadoSenha = "x"
        };

        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.Ambiente);
    }
}
