using FiscalService.Api.Models.Requests;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.Common.Types;

namespace FiscalService.Api.Services.NFSe;

/// <summary>Mapeia DTOs REST → modelo OpenAC (sem I/O).</summary>
public static class NFSeDpsMapper
{
    public const string ModeloNumeracao = "NS";

    public static Dps MontarDps(
        NFSeEmitirRequest request,
        ConfiguracaoEmitenteRequest emitente,
        EmitenteNfseContexto ctx,
        int numeroDps,
        VersaoNFSe versao,
        DFeTipoAmbiente ambiente)
    {
        var codMunEmitente = ctx.CodigoMunicipioIbge
                             ?? emitente.Endereco?.CodigoMunicipio
                             ?? throw new ArgumentException("codigoMunicipio do emitente é obrigatório.");

        var codMunPrestacao = request.Servico.CodigoMunicipioPrestacao
                              ?? codMunEmitente;

        var emailPrestador = request.Prestador?.Email
                             ?? ctx.Email
                             ?? throw new ArgumentException("E-mail do prestador é obrigatório (cadastro ou request.prestador.email).");

        var dps = new Dps
        {
            Versao = versao,
            Informacoes = new InfDps
            {
                TipoAmbiente = ambiente,
                DhEmissao = DateTime.Now,
                LocalidadeEmitente = SomenteDigitos(codMunEmitente),
                Serie = request.Serie,
                NumeroDps = numeroDps.ToString(),
                Competencia = request.Competencia.Date,
                TipoEmitente = EmitenteDps.Prestador,
                Prestador = new PrestadorDps
                {
                    CNPJ = emitente.Cnpj,
                    Email = emailPrestador,
                    InscricaoMunicipal = request.Prestador?.InscricaoMunicipal ?? ctx.InscricaoMunicipal,
                    Regime = new RegimeTributario
                    {
                        OptanteSimplesNacional = emitente.Crt is 1 or 2
                            ? OptanteSimplesNacional.OptanteMEEPP
                            : OptanteSimplesNacional.NaoOptante,
                        RegimeEspecial = RegimeEspecial.Nenhum
                    }
                },
                Tomador = MapearTomador(request.Tomador),
                Servico = new ServicoNFSe
                {
                    Localidade = new LocalidadeNFSe
                    {
                        CodMunicipioPrestacao = SomenteDigitos(codMunPrestacao)
                    },
                    Informacoes = new InformacoesServico
                    {
                        CodTributacaoNacional = request.Servico.CodTributacaoNacional,
                        CodTributacaoMunicipio = request.Servico.CodTributacaoMunicipio,
                        Descricao = request.Servico.Descricao
                    }
                },
                Valores = new ValoresDps
                {
                    ValoresServico = new ValoresServico
                    {
                        Valor = request.Valores.ValorServico
                    },
                    Tributos = new TributosNFSe
                    {
                        Municipal = new TributoMunicipal
                        {
                            ISSQN = ResolverIssqn(request.Valores.Issqn),
                            TipoRetencaoISSQN = ResolverRetencaoIss(request.Valores.TipoRetencaoIssqn),
                            Aliquota = request.Valores.AliquotaIss
                        }
                    }
                }
            }
        };

        return dps;
    }

    public static PedidoRegistroEvento MontarCancelamento(
        NFSeCancelarRequest request,
        ConfiguracaoEmitenteRequest emitente,
        VersaoNFSe versao,
        DFeTipoAmbiente ambiente)
    {
        return new PedidoRegistroEvento
        {
            Versao = versao,
            Informacoes = new InfPedReg
            {
                TipoAmbiente = ambiente,
                DhEvento = DateTimeOffset.Now,
                ChNFSe = request.ChaveAcesso,
                CNPJAutor = emitente.Cnpj,
                Evento = new EventoCancelamento
                {
                    CodMotivo = ResolverMotivoCancelamento(request.CodigoMotivo),
                    Descricao = request.DescricaoMotivo
                }
            }
        };
    }

    private static InfoPessoaNFSe MapearTomador(NFSeTomadorRequest tomador)
    {
        var pessoa = new InfoPessoaNFSe
        {
            Nome = tomador.Nome,
            Email = tomador.Email,
            Endereco = new EnderecoNFSe
            {
                Logradouro = tomador.Endereco.Logradouro,
                Numero = tomador.Endereco.Numero,
                Complemento = tomador.Endereco.Complemento,
                Bairro = tomador.Endereco.Bairro,
                Municipio = new MunicipioNacional
                {
                    CodMunicipio = SomenteDigitos(tomador.Endereco.CodigoMunicipio!),
                    CEP = SomenteDigitos(tomador.Endereco.Cep ?? "")
                }
            }
        };

        var cnpj = SomenteDigitos(tomador.Cnpj);
        var cpf = SomenteDigitos(tomador.Cpf);
        if (cnpj.Length == 14)
            pessoa.CNPJ = cnpj;
        else if (cpf.Length == 11)
            pessoa.CPF = cpf;

        return pessoa;
    }

    private static TributoISSQN ResolverIssqn(string valor) =>
        valor.Equals("NaoTributavel", StringComparison.OrdinalIgnoreCase)
            || valor.Equals("NaoIncidencia", StringComparison.OrdinalIgnoreCase)
            ? TributoISSQN.NaoIncidencia
            : TributoISSQN.OperacaoTributavel;

    private static TipoRetencaoISSQN ResolverRetencaoIss(string valor) =>
        valor.Equals("RetidoTomador", StringComparison.OrdinalIgnoreCase)
            ? TipoRetencaoISSQN.RetidoTomador
            : TipoRetencaoISSQN.NaoRetido;

    private static MotivoCancelamento ResolverMotivoCancelamento(string valor) => valor switch
    {
        "ServicoNaoPrestado" => MotivoCancelamento.ServicoNaoPrestado,
        "Outros" => MotivoCancelamento.Outros,
        _ => MotivoCancelamento.ErroEmissao
    };

    private static string SomenteDigitos(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());
}
