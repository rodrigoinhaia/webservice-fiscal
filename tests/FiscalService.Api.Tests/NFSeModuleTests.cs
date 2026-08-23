using FiscalService.Api.Config;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using FiscalService.Api.Services.NFSe;
using FiscalService.Api.Validation;
using FluentValidation.TestHelper;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFSe.Nacional.Common.Types;
using System.Net;
using Xunit;

namespace FiscalService.Api.Tests;

public class NFSeEmitirRequestValidatorTests
{
    private readonly NFSeEmitirRequestValidator _validator = new(new ConfiguracaoEmitenteRequestValidator());

    [Fact]
    public void Request_valido_passa()
    {
        var model = RequestValido();
        var r = _validator.TestValidate(model);
        r.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Cod_tributacao_nacional_invalido_falha()
    {
        var model = RequestValido();
        model.Servico.CodTributacaoNacional = "ABC";
        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.Servico.CodTributacaoNacional);
    }

    [Fact]
    public void Tomador_sem_documento_falha()
    {
        var model = RequestValido();
        model.Tomador.Cnpj = null;
        model.Tomador.Cpf = null;
        var r = _validator.TestValidate(model);
        r.ShouldHaveAnyValidationError();
    }

    private static NFSeEmitirRequest RequestValido() => new()
    {
        EmitenteCnpj = "12345678000190",
        Competencia = new DateTime(2026, 8, 1),
        Tomador = new NFSeTomadorRequest
        {
            Cnpj = "98765432000100",
            Nome = "Tomador",
            Endereco = new EnderecoRequest
            {
                Logradouro = "Rua A",
                Numero = "1",
                Bairro = "Centro",
                CodigoMunicipio = "4314902",
                Cep = "90000000"
            }
        },
        Servico = new NFSeServicoRequest
        {
            CodTributacaoNacional = "010101",
            Descricao = "Servico de software"
        },
        Valores = new NFSeValoresRequest { ValorServico = 100m },
        Prestador = new NFSePrestadorRequest { Email = "a@b.com" }
    };
}

public class NFSeCancelarRequestValidatorTests
{
    private readonly NFSeCancelarRequestValidator _validator = new(new ConfiguracaoEmitenteRequestValidator());

    [Fact]
    public void Chave_44_digitos_falha()
    {
        var model = new NFSeCancelarRequest
        {
            EmitenteCnpj = "12345678000190",
            ChaveAcesso = new string('1', 44),
            DescricaoMotivo = "Motivo com 15 chars"
        };
        var r = _validator.TestValidate(model);
        r.ShouldHaveValidationErrorFor(x => x.ChaveAcesso);
    }

    [Fact]
    public void Chave_50_digitos_passa()
    {
        var model = new NFSeCancelarRequest
        {
            EmitenteCnpj = "12345678000190",
            ChaveAcesso = new string('1', 50),
            DescricaoMotivo = "Motivo com 15 chars"
        };
        var r = _validator.TestValidate(model);
        r.ShouldNotHaveValidationErrorFor(x => x.ChaveAcesso);
    }
}

public class NFSeDpsMapperTests
{
    [Fact]
    public void MontarDps_preenche_prestador_tomador_e_servico()
    {
        var emitente = new ConfiguracaoEmitenteRequest
        {
            Cnpj = "12345678000190",
            RazaoSocial = "Prestador",
            Crt = 1,
            Uf = "RS",
            Ambiente = "Homologacao",
            CertificadoPath = "c.pfx",
            CertificadoSenha = "s",
            Endereco = new EnderecoRequest { CodigoMunicipio = "4314902" }
        };

        var request = new NFSeEmitirRequest
        {
            Serie = "1",
            Competencia = new DateTime(2026, 8, 1),
            Prestador = new NFSePrestadorRequest { Email = "prestador@empresa.com" },
            Tomador = new NFSeTomadorRequest
            {
                Cnpj = "98765432000100",
                Nome = "Tomador",
                Endereco = new EnderecoRequest
                {
                    Logradouro = "Rua B",
                    Numero = "10",
                    Bairro = "Centro",
                    CodigoMunicipio = "4314902",
                    Cep = "90000000"
                }
            },
            Servico = new NFSeServicoRequest
            {
                CodTributacaoNacional = "140201",
                Descricao = "Desenvolvimento"
            },
            Valores = new NFSeValoresRequest { ValorServico = 1500m }
        };

        var ctx = new EmitenteNfseContexto
        {
            CodigoMunicipioIbge = "4314902",
            Email = "cadastro@empresa.com"
        };

        var dps = NFSeDpsMapper.MontarDps(
            request, emitente, ctx, 42, VersaoNFSe.Ve101, DFeTipoAmbiente.Homologacao);

        Assert.Equal(VersaoNFSe.Ve101, dps.Versao);
        Assert.Equal("42", dps.Informacoes.NumeroDps);
        Assert.Equal("12345678000190", dps.Informacoes.Prestador.CNPJ);
        Assert.Equal("98765432000100", dps.Informacoes.Tomador.CNPJ);
        Assert.Equal(OptanteSimplesNacional.OptanteMEEPP, dps.Informacoes.Prestador.Regime.OptanteSimplesNacional);
        Assert.Equal(RegimeApuracao.TributosFederaisMunicipalSN, dps.Informacoes.Prestador.Regime.RegimeApuracao);
        Assert.Equal("120012000", dps.Informacoes.Servico.Informacoes.CodNBS);
        Assert.NotNull(dps.Informacoes.IBSCBS);
        Assert.Equal(RTCFinNFSe.Regular, dps.Informacoes.IBSCBS.FinalidadeNFSe);
        Assert.Equal(1500m, dps.Informacoes.Valores.ValoresServico.Valor);
        Assert.Equal(0, dps.Informacoes.Valores.Tributos.Total!.IndicadorTotal);
    }

    [Fact]
    public void MontarCancelamento_usa_ChNFSe_e_CNPJAutor()
    {
        var chave = new string('9', 50);
        var evento = NFSeDpsMapper.MontarCancelamento(
            new NFSeCancelarRequest
            {
                ChaveAcesso = chave,
                CodigoMotivo = "ErroEmissao",
                DescricaoMotivo = "Erro na descricao do servico."
            },
            new ConfiguracaoEmitenteRequest
            {
                Cnpj = "12345678000190",
                RazaoSocial = "X",
                Uf = "RS",
                CertificadoPath = "c.pfx",
                CertificadoSenha = "s"
            },
            VersaoNFSe.Ve101,
            DFeTipoAmbiente.Homologacao);

        Assert.Equal(chave, evento.Informacoes.ChNFSe);
        Assert.Equal("12345678000190", evento.Informacoes.CNPJAutor);
    }
}

public class NFSeOpenAcFactoryTests
{
    [Fact]
    public void ResolverVersaoDps_Ve101()
    {
        Assert.Equal(VersaoNFSe.Ve101, NFSeOpenAcFactory.ResolverVersaoDps("Ve101"));
        Assert.Equal(VersaoNFSe.Ve100, NFSeOpenAcFactory.ResolverVersaoDps("Ve100"));
    }

    [Fact]
    public void ResolverProtocolosTls_exclui_tls10_e_tls11()
    {
        var protocolos = NFSeOpenAcFactory.ResolverProtocolosTls();
        Assert.True(protocolos.HasFlag(SecurityProtocolType.Tls12));
        Assert.True(protocolos.HasFlag(SecurityProtocolType.Tls13));
        Assert.False(protocolos.HasFlag(SecurityProtocolType.Tls));
        Assert.False(protocolos.HasFlag(SecurityProtocolType.Tls11));
    }

    [Fact]
    public void Criar_aponta_schemas_versao_101()
    {
        var repoSchemas = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "src", "FiscalService.Api", "Schemas", "NFSe"));
        var fiscal = new FiscalConfig
        {
            DiretorioXmls = Path.GetTempPath(),
            NFSe = new NfseConfig { VersaoDps = "Ve101", DiretorioSchemas = repoSchemas }
        };
        var factory = new NFSeOpenAcFactory(fiscal, new CertificadoService(fiscal, Microsoft.Extensions.Logging.Abstractions.NullLogger<CertificadoService>.Instance));
        var emitente = new ConfiguracaoEmitenteRequest
        {
            Cnpj = "12345678000190",
            RazaoSocial = "X",
            Uf = "RS",
            Ambiente = "Homologacao",
            CertificadoPath = "inexistente.pfx",
            CertificadoSenha = "s",
            Endereco = new EnderecoRequest { CodigoMunicipio = "4314902" }
        };

        var open = factory.Criar(emitente, new EmitenteNfseContexto { CodigoMunicipioIbge = "4314902" });
        Assert.Equal(NFSeOpenAcFactory.ResolverProtocolosTls(), open.Configuracoes.WebServices.Protocolos);
        Assert.EndsWith(Path.Combine("1.01"), open.Configuracoes.Arquivos.PathSchemas, StringComparison.OrdinalIgnoreCase);
    }
}
