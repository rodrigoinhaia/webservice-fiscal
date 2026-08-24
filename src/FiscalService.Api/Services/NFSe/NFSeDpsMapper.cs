using System.Globalization;
using System.Text;
using FiscalService.Api.Models.Requests;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.Common.Types;

namespace FiscalService.Api.Services.NFSe;

/// <summary>Mapeia DTOs REST → modelo OpenAC (sem I/O).</summary>
public static class NFSeDpsMapper
{
    public const string ModeloNumeracao = "NS";

    private static readonly TimeZoneInfo FusoBrasil = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");

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

        var optanteSn = emitente.Crt is 1 or 2;
        var codNbs = ResolverCodNbs(request);

        var dps = new Dps
        {
            Versao = versao,
            Informacoes = new InfDps
            {
                TipoAmbiente = ambiente,
                DhEmissao = ObterDataHoraBrasil(),
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
                        OptanteSimplesNacional = optanteSn
                            ? OptanteSimplesNacional.OptanteMEEPP
                            : OptanteSimplesNacional.NaoOptante,
                        RegimeApuracao = optanteSn
                            ? RegimeApuracao.TributosFederaisMunicipalSN
                            : default,
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
                        CodNBS = codNbs,
                        Descricao = SanitizarDescricao(request.Servico.Descricao)
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
                        },
                        Total = MontarTotalTributos(optanteSn, request.Valores)
                    }
                },
                IBSCBS = MontarIbscbs(request, versao)
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
        const string tipoEventoCancelamento = "101101";
        var chave = SomenteDigitos(request.ChaveAcesso);
        if (chave.Length != 50)
            throw new ArgumentException("chaveAcesso da NFS-e deve ter 50 dígitos.");

        return new PedidoRegistroEvento
        {
            Versao = versao,
            Informacoes = new InfPedReg
            {
                // TSIdPedRegEvt: PRE + chave(50) + tipoEvento(6) = 59
                Id = $"PRE{chave}{tipoEventoCancelamento}",
                TipoAmbiente = ambiente,
                DhEvento = ObterDataHoraBrasil(),
                ChNFSe = request.ChaveAcesso,
                CNPJAutor = emitente.Cnpj,
                Evento = new EventoCancelamento
                {
                    CodMotivo = ResolverMotivoCancelamento(request.CodigoMotivo),
                    Descricao = SanitizarDescricao(request.DescricaoMotivo)
                }
            }
        };
    }

    private static RTCInfoIBSCBS? MontarIbscbs(NFSeEmitirRequest request, VersaoNFSe versao)
    {
        if (versao != VersaoNFSe.Ve101)
            return null;

        var rtc = request.ReformaTributaria;
        var indFinal = rtc?.IndicadorUsoFinal ?? false;

        return new RTCInfoIBSCBS
        {
            FinalidadeNFSe = RTCFinNFSe.Regular,
            IndicadorUsoFinal = indFinal ? RTCIndFinal.Sim : RTCIndFinal.Nao,
            CodigoIndicadorOperacao = rtc?.CodigoIndicadorOperacao ?? "030101",
            IndicadorDestinatario = RTCIndDest.ProprioTomador,
            Valores = new RTCInfoValoresIBSCBS
            {
                Tributos = new RTCInfoTributosIBSCBS
                {
                    GrupoIBSCBS = new RTCInfoTributosSitClas
                    {
                        CodigoSituacaoTributaria = rtc?.CodigoSituacaoTributaria ?? "000",
                        CodigoClassificacaoTributaria = rtc?.CodigoClassificacaoTributaria ?? "000001"
                    }
                }
            }
        };
    }

    /// <summary>
    /// ME/EPP (Simples): pTotTribSN. Demais: indTotTrib=0.
    /// totTrib é obrigatório no XSD; choice vazio gera E1235.
    /// </summary>
    private static TotalTributos MontarTotalTributos(bool optanteSn, NFSeValoresRequest valores)
    {
        if (!optanteSn)
            return new TotalTributos { IndicadorTotal = 0 };

        var pTotTribSn = valores.PercentualTotalTributosSimples ?? 6.00m;
        return new TotalTributos { PercetualSimples = pTotTribSn };
    }

    private static string? ResolverCodNbs(NFSeEmitirRequest request)
    {
        var informado = SomenteDigitos(request.Servico.CodNbs);
        if (informado.Length == 9)
            return informado;

        return request.Servico.CodTributacaoNacional switch
        {
            "140201" => "120012000",
            _ => string.IsNullOrEmpty(informado) ? null : informado
        };
    }

    private static DateTimeOffset ObterDataHoraBrasil()
    {
        var agoraUtc = DateTime.UtcNow;
        var offset = FusoBrasil.GetUtcOffset(agoraUtc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, FusoBrasil);
        return new DateTimeOffset(local, offset);
    }

    private static string SanitizarDescricao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var normalizado = texto.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(normalizado.Length);
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(c switch
            {
                '—' or '–' or '−' => '-',
                '“' or '”' => '"',
                '‘' or '’' => '\'',
                '…' => '.',
                _ => c
            });
        }

        return sb.ToString().Trim();
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
