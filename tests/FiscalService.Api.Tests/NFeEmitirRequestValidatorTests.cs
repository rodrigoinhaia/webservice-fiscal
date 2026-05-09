using FiscalService.Api.Models.Requests;
using FiscalService.Api.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace FiscalService.Api.Tests;

public class NFeEmitirRequestValidatorTests
{
    private readonly NFeEmitirRequestValidator _validator = new(
        new ConfiguracaoEmitenteRequestValidator(),
        new ItemNFeRequestValidator());

    [Fact]
    public void Sem_itens_falha()
    {
        var model = new NFeEmitirRequest
        {
            ConfiguracaoEmitente = EmitenteValido(),
            NumeroNota = 1,
            Serie = "1",
            Destinatario = new DestinatarioRequest { Cpf = "00000000000", RazaoSocial = "CF", IndicadorIe = 9 },
            Itens = new List<ItemNFeRequest>()
        };

        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.Itens);
    }

    private static ConfiguracaoEmitenteRequest EmitenteValido() => new()
    {
        Cnpj = "12345678000190",
        RazaoSocial = "Emitente",
        Uf = "RS",
        Ambiente = "Homologacao",
        CertificadoPath = "c.pfx",
        CertificadoSenha = "s"
    };
}
