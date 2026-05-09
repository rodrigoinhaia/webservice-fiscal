using FiscalService.Api.Models.Requests;
using FiscalService.Api.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace FiscalService.Api.Tests;

public class MDFeEmitirRequestValidatorTests
{
    private readonly MDFeEmitirRequestValidator _validator = new(new ConfiguracaoEmitenteRequestValidator());

    [Theory]
    [InlineData("01", true)]
    [InlineData("02", true)]
    [InlineData("03", true)]
    [InlineData("04", true)]
    [InlineData("05", false)]
    [InlineData("1", false)]
    public void Modal_aceita_01_a_04(string modal, bool valido)
    {
        var model = MDFeValido();
        model.Modal = modal;
        var r = _validator.TestValidate(model);

        if (valido) r.ShouldNotHaveValidationErrorFor(x => x.Modal);
        else r.ShouldHaveValidationErrorFor(x => x.Modal);
    }

    [Fact]
    public void MDFe_sem_municipios_carregamento_falha()
    {
        var model = MDFeValido();
        model.MunicipiosCarregamento.Clear();
        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.MunicipiosCarregamento);
    }

    [Fact]
    public void MDFe_sem_documentos_falha()
    {
        var model = MDFeValido();
        model.Documentos.Clear();
        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.Documentos);
    }

    [Theory]
    [InlineData("RS", true)]
    [InlineData("rs", true)]
    [InlineData("R1", false)]
    [InlineData("BRA", false)]
    public void UfInicio_2_letras(string uf, bool valido)
    {
        var model = MDFeValido();
        model.UfInicio = uf;
        var r = _validator.TestValidate(model);

        if (valido) r.ShouldNotHaveValidationErrorFor(x => x.UfInicio);
        else r.ShouldHaveValidationErrorFor(x => x.UfInicio);
    }

    private static MDFeEmitirRequest MDFeValido() => new()
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
        Modal = "01",
        UfInicio = "RS",
        UfFim = "SC",
        MunicipiosCarregamento = new List<MunicipioCarregamentoRequest>
        {
            new() { CodigoMunicipio = "4314902", NomeMunicipio = "Porto Alegre" }
        },
        Documentos = new List<DocumentoMDFeRequest>
        {
            new()
            {
                ChaveAcesso = new string('1', 44),
                TipoDocumento = "CTe",
                CodigoMunicipioDescarga = "4205407",
                NomeMunicipioDescarga = "Florianopolis"
            }
        }
    };
}
