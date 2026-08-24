using FiscalService.Api.Config;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using FiscalService.Api.Services.NFSe;
using FiscalService.Api.Validation;
using FluentValidation.TestHelper;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;
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
        Assert.Equal(6.00m, dps.Informacoes.Valores.Tributos.Total!.PercetualSimples);
        Assert.Null(dps.Informacoes.Valores.Tributos.Total.IndicadorTotal);
    }

    [Fact]
    public void MontarDps_nao_optante_sn_informa_indTotTrib()
    {
        var emitente = new ConfiguracaoEmitenteRequest
        {
            Cnpj = "12345678000190",
            RazaoSocial = "Prestador",
            Crt = 3,
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

        var dps = NFSeDpsMapper.MontarDps(
            request,
            emitente,
            new EmitenteNfseContexto { CodigoMunicipioIbge = "4314902" },
            1,
            VersaoNFSe.Ve101,
            DFeTipoAmbiente.Homologacao);

        Assert.Equal(OptanteSimplesNacional.NaoOptante, dps.Informacoes.Prestador.Regime.OptanteSimplesNacional);
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
        Assert.Equal($"PRE{chave}101101", evento.Informacoes.Id);
        var cancelamento = Assert.IsType<EventoCancelamento>(evento.Informacoes.Evento);
        Assert.Equal("Cancelamento de NFS-e", cancelamento.Descricao);
        Assert.Equal("Erro na descricao do servico.", cancelamento.Motivo);
        Assert.Equal(MotivoCancelamento.ErroEmissao, cancelamento.CodMotivo);
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

public class NFSeDanfseLocalRendererTests
{
    private static readonly string XmlNfseMinimo = """
        <?xml version="1.0" encoding="UTF-8"?>
        <NFSe xmlns="http://www.sped.fazenda.gov.br/nfse" versao="1.01">
          <infNFSe Id="NFS43051041253117200017000000000000000000000000000001">
            <xLocEmi>PORTO ALEGRE</xLocEmi>
            <xLocPrestacao>PORTO ALEGRE</xLocPrestacao>
            <nNFSe>1</nNFSe>
            <cLocIncid>4314902</cLocIncid>
            <xLocIncid>PORTO ALEGRE</xLocIncid>
            <xTribNac>Analise e desenvolvimento de sistemas</xTribNac>
            <verAplic>FiscalService</verAplic>
            <ambGer>1</ambGer>
            <tpEmis>1</tpEmis>
            <cStat>100</cStat>
            <dhProc>2026-08-23T10:00:00-03:00</dhProc>
            <nDFSe>1</nDFSe>
            <emit>
              <CNPJ>12531172000170</CNPJ>
              <xNome>Samuel Diehl e Cia Ltda</xNome>
              <enderNac>
                <xLgr>Rua Teste</xLgr>
                <nro>100</nro>
                <xBairro>Centro</xBairro>
                <cMun>4314902</cMun>
                <UF>RS</UF>
                <CEP>90000000</CEP>
              </enderNac>
            </emit>
            <valores>
              <vLiq>100.00</vLiq>
            </valores>
            <DPS versao="1.01">
              <infDPS Id="DPS431490212531172000170001000000000000001">
                <tpAmb>1</tpAmb>
                <dhEmi>2026-08-23T10:00:00-03:00</dhEmi>
                <verAplic>FiscalService</verAplic>
                <serie>1</serie>
                <nDPS>1</nDPS>
                <dCompet>2026-08-01</dCompet>
                <tpEmit>1</tpEmit>
                <cLocEmi>4314902</cLocEmi>
                <prest>
                  <CNPJ>12531172000170</CNPJ>
                  <regTrib>
                    <opSimpNac>3</opSimpNac>
                    <regApTribSN>1</regApTribSN>
                    <regEspTrib>0</regEspTrib>
                  </regTrib>
                </prest>
                <toma>
                  <CNPJ>98765432000100</CNPJ>
                  <xNome>Tomador Teste</xNome>
                  <end>
                    <endNac>
                      <cMun>4314902</cMun>
                      <CEP>90000000</CEP>
                    </endNac>
                    <xLgr>Rua A</xLgr>
                    <nro>1</nro>
                    <xBairro>Centro</xBairro>
                  </end>
                </toma>
                <serv>
                  <locPrest>
                    <cLocPrestacao>4314902</cLocPrestacao>
                  </locPrest>
                  <cServ>
                    <cTribNac>010101</cTribNac>
                    <xDescServ>Servico de software</xDescServ>
                  </cServ>
                </serv>
                <valores>
                  <vServPrest>
                    <vServ>100.00</vServ>
                  </vServPrest>
                  <trib>
                    <tribMun>
                      <tribISSQN>1</tribISSQN>
                      <tpRetISSQN>2</tpRetISSQN>
                    </tribMun>
                    <totTrib>
                      <pTotTribSN>6.00</pTotTribSN>
                    </totTrib>
                  </trib>
                </valores>
              </infDPS>
            </DPS>
          </infNFSe>
        </NFSe>
        """;

    [Fact]
    public void GerarDeXml_produz_pdf_valido()
    {
        var renderer = new NFSeDanfseLocalRenderer(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NFSeDanfseLocalRenderer>.Instance);

        var pdf = renderer.TentarGerarDeXml(XmlNfseMinimo, homologacao: false);

        Assert.NotNull(pdf);
        Assert.True(pdf!.Length > 1000);
        Assert.Equal(0x25, pdf[0]); // %
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void NormalizarXml_extrai_NFSe_de_envelope()
    {
        var corpo = XmlNfseMinimo.Replace("""<?xml version="1.0" encoding="UTF-8"?>""", string.Empty).Trim();
        var envelope = $"<raiz><outro>x</outro>{corpo}</raiz>";
        var xml = NFSeDanfseLocalRenderer.NormalizarXmlNfse(envelope);
        Assert.NotNull(xml);
        Assert.Contains("<NFSe", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<raiz>", xml, StringComparison.Ordinal);
    }
}

public class NFSeDpsXmlNormalizerTests
{
    [Fact]
    public void RemoverXmlnsVazioInfAssinado_remove_apenas_no_infDPS()
    {
        const string xml = """<infDPS Id="DPS1" xmlns=""><nDPS>1</nDPS></infDPS>""";

        var normalizado = NFSeDpsXmlNormalizer.RemoverXmlnsVazioInfAssinado(xml);

        Assert.Equal("""<infDPS Id="DPS1"><nDPS>1</nDPS></infDPS>""", normalizado);
    }

    [Fact]
    public void AjustarXmlAposAssinatura_remove_assinatura_vazia_e_forca_utf8()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-16"?>
            <DPS versao="1.01" xmlns="http://www.sped.fazenda.gov.br/nfse">
              <infDPS Id="DPS1" xmlns="">
                <nDPS>1</nDPS>
              </infDPS>
              <Signature xmlns="http://www.w3.org/2000/09/xmldsig#"></Signature>
              <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
                <SignedInfo>
                  <Reference URI="#DPS1">
                    <DigestValue>abc=</DigestValue>
                  </Reference>
                </SignedInfo>
                <SignatureValue>def=</SignatureValue>
              </Signature>
            </DPS>
            """;

        var normalizado = NFSeDpsXmlNormalizer.AjustarXmlAposAssinatura(xml);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", normalizado, StringComparison.Ordinal);
        Assert.DoesNotContain("encoding=\"utf-16\"", normalizado, StringComparison.OrdinalIgnoreCase);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(normalizado, @"<Signature[\s>]"));
    }
}
