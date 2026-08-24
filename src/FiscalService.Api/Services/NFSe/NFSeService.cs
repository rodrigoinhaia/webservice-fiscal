using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services.Fiscal;
using Microsoft.EntityFrameworkCore;
using OpenAC.Net.NFSe.Nacional;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.Common.Types;

namespace FiscalService.Api.Services.NFSe;

/// <summary>
/// Orquestra emissão, cancelamento, consulta e DANFSe via OpenAC NFS-e Nacional.
/// ATENÇÃO: Transient — OpenNFSeNacional não é thread-safe.
/// </summary>
public sealed class NFSeService
{
    private readonly FiscalConfig _fiscalConfig;
    private readonly AppDbContext _db;
    private readonly NumeracaoService _numeracaoService;
    private readonly EmitenteService _emitenteService;
    private readonly NFSeOpenAcFactory _openAcFactory;
    private readonly NFSeDanfseLocalRenderer _danfseLocal;
    private readonly ILogger<NFSeService> _logger;

    public NFSeService(
        FiscalConfig fiscalConfig,
        AppDbContext db,
        NumeracaoService numeracaoService,
        EmitenteService emitenteService,
        NFSeOpenAcFactory openAcFactory,
        NFSeDanfseLocalRenderer danfseLocal,
        ILogger<NFSeService> logger)
    {
        _fiscalConfig = fiscalConfig;
        _db = db;
        _numeracaoService = numeracaoService;
        _emitenteService = emitenteService;
        _openAcFactory = openAcFactory;
        _danfseLocal = danfseLocal;
        _logger = logger;
    }

    public async Task<FiscalResponse> EmitirAsync(NFSeEmitirRequest request, CancellationToken ct = default)
    {
        try
        {
            request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
            var ctx = await ObterContextoNfseAsync(request.ConfiguracaoEmitente.Cnpj, ct);
            ValidarPrecondicoesEmitente(request.ConfiguracaoEmitente, ctx, request);

            var numero = request.NumeroDps
                         ?? await _numeracaoService.ObterProximoNumeroAsync(
                             request.ConfiguracaoEmitente.Cnpj,
                             NFSeDpsMapper.ModeloNumeracao,
                             request.Serie,
                             request.ConfiguracaoEmitente.Ambiente,
                             ct);

            var versao = NFSeOpenAcFactory.ResolverVersaoDps(_fiscalConfig.NFSe.VersaoDps);
            var ambiente = NFSeOpenAcFactory.ResolverAmbiente(request.ConfiguracaoEmitente.Ambiente);

            var dps = NFSeDpsMapper.MontarDps(request, request.ConfiguracaoEmitente, ctx, numero, versao, ambiente);
            var openAc = _openAcFactory.Criar(request.ConfiguracaoEmitente, ctx);

            using var certificado = _openAcFactory.CarregarCertificadoEmitente(request.ConfiguracaoEmitente);
            NFSeDpsXmlNormalizer.AssinarDps(dps, openAc.Configuracoes, certificado);

            var retorno = await SefazRetry.ExecuteAsync(_fiscalConfig, _logger, "NFSeEnviar", () =>
                openAc.EnviarAsync(dps));

            if (!retorno.Sucesso)
            {
                var erros = NFSeOpenAcResponseHelper.FormatarErros(retorno.Resultado?.Erros);
                _logger.LogWarning("NFS-e rejeitada: {Erros}", erros);
                return FiscalResponse.Falha("RejeicaoNfseNacional", erros,
                    xmlEnviado: retorno.XmlEnvio, xmlRetorno: retorno.XmlRetorno);
            }

            var resultado = retorno.Resultado!;
            var chave = resultado.ChaveAcesso ?? string.Empty;
            var protocolo = resultado.IdDps ?? string.Empty;
            var mensagem = NFSeOpenAcResponseHelper.FormatarSucesso("NFS-e autorizada.", resultado.Alertas);

            await RegistrarLogAsync(
                request.ConfiguracaoEmitente.Cnpj,
                request.Serie,
                numero,
                chave,
                protocolo,
                "Autorizado",
                "100",
                mensagem,
                request.ConfiguracaoEmitente.Ambiente,
                ct);

            await SincronizarNumeracaoAsync(
                request.ConfiguracaoEmitente.Cnpj,
                request.Serie,
                numero,
                request.ConfiguracaoEmitente.Ambiente,
                ct);

            var homologacao = !EhProducao(request.ConfiguracaoEmitente.Ambiente);
            var xmlAutorizado = resultado.XmlNFSe
                                ?? retorno.XmlRetorno;
            var pdfBase64 = await ObterDanfseBase64AposEmissaoAsync(
                openAc, chave, resultado.NFSe, xmlAutorizado, homologacao);

            _logger.LogInformation("NFS-e autorizada: Chave={Chave}", chave);
            return FiscalResponse.Ok(chave, protocolo, "100", mensagem,
                xml: xmlAutorizado ?? retorno.XmlRetorno, pdf: pdfBase64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao emitir NFS-e para CNPJ={CNPJ}",
                request.ConfiguracaoEmitente?.Cnpj ?? request.EmitenteCnpj);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> CancelarAsync(NFSeCancelarRequest request, CancellationToken ct = default)
    {
        try
        {
            request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
            var ctx = await ObterContextoNfseAsync(request.ConfiguracaoEmitente.Cnpj, ct);

            var versao = NFSeOpenAcFactory.ResolverVersaoDps(_fiscalConfig.NFSe.VersaoDps);
            var ambiente = NFSeOpenAcFactory.ResolverAmbiente(request.ConfiguracaoEmitente.Ambiente);

            var evento = NFSeDpsMapper.MontarCancelamento(
                request, request.ConfiguracaoEmitente, versao, ambiente);

            var openAc = _openAcFactory.Criar(request.ConfiguracaoEmitente, ctx);
            using var certificado = _openAcFactory.CarregarCertificadoEmitente(request.ConfiguracaoEmitente);
            NFSeDpsXmlNormalizer.AssinarEvento(evento, openAc.Configuracoes, certificado);

            var retorno = await SefazRetry.ExecuteAsync(_fiscalConfig, _logger, "NFSeCancelar", () =>
                openAc.EnviarEventoAsync(evento));

            if (!retorno.Sucesso)
            {
                var erros = NFSeOpenAcResponseHelper.FormatarErros(retorno.Resultado?.Erros);
                return FiscalResponse.Falha("RejeicaoNfseNacional", erros,
                    xmlEnviado: retorno.XmlEnvio, xmlRetorno: retorno.XmlRetorno);
            }

            var protocolo = retorno.Resultado?.DataHoraProcessamento.ToString("O") ?? string.Empty;
            var mensagem = NFSeOpenAcResponseHelper.FormatarSucesso("NFS-e cancelada.", retorno.Resultado?.Alertas);

            await AtualizarLogCanceladoAsync(request.ChaveAcesso, protocolo, mensagem, ct);

            return FiscalResponse.Ok(request.ChaveAcesso, protocolo, "135", mensagem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar NFS-e Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> ConsultarAsync(NFSeConsultarRequest request, CancellationToken ct = default)
    {
        try
        {
            request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
            var ctx = await ObterContextoNfseAsync(request.ConfiguracaoEmitente.Cnpj, ct);
            var openAc = _openAcFactory.Criar(request.ConfiguracaoEmitente, ctx);

            var retorno = await SefazRetry.ExecuteAsync(_fiscalConfig, _logger, "NFSeConsultaChave", () =>
                openAc.ConsultaChaveAsync(request.ChaveAcesso));

            var resultado = retorno.Resultado;
            if (resultado is not null)
            {
                var status = resultado.StatusProcessamento;
                if (status == StatusProcessamentoDistribuicao.DOCUMENTOS_LOCALIZADOS)
                {
                    var doc = resultado.Lote?.FirstOrDefault(d =>
                        string.Equals(d.ChaveAcesso, request.ChaveAcesso, StringComparison.Ordinal));
                    return FiscalResponse.Ok(
                        doc?.ChaveAcesso ?? request.ChaveAcesso,
                        doc?.NSU.ToString() ?? string.Empty,
                        status.ToString(),
                        NFSeOpenAcResponseHelper.FormatarSucesso(status.ToString(), resultado.Alertas),
                        xml: doc?.ArquivoXml);
                }

                var detalheStatus = status switch
                {
                    StatusProcessamentoDistribuicao.NENHUM_DOCUMENTO_LOCALIZADO =>
                        "Nenhum DF-e localizado para a chave (ADN pode atrasar alguns segundos após a autorização).",
                    StatusProcessamentoDistribuicao.REJEICAO =>
                        NFSeOpenAcResponseHelper.FormatarErros(resultado.Erros),
                    _ => status.ToString()
                };

                return FiscalResponse.Falha("ConsultaNfseNacional", detalheStatus,
                    xmlRetorno: retorno.XmlRetorno);
            }

            if (!retorno.Sucesso)
            {
                var erros = NFSeOpenAcResponseHelper.FormatarErros(null);
                return FiscalResponse.Falha("ConsultaNfseNacional", erros,
                    xmlRetorno: retorno.XmlRetorno);
            }

            return FiscalResponse.Falha("ConsultaNfseNacional", "Retorno de consulta sem resultado.",
                xmlRetorno: retorno.XmlRetorno);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar NFS-e Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> DownloadDanfseAsync(
        string chaveAcesso,
        IEmitenteConfigSource emitenteSource,
        CancellationToken ct = default)
    {
        try
        {
            if (chaveAcesso.Length != 50 || !chaveAcesso.All(char.IsDigit))
                return FiscalResponse.Falha("Validacao", "Chave de acesso NFS-e deve ter 50 dígitos numéricos.");

            var config = await _emitenteService.ResolverConfiguracaoAsync(emitenteSource, ct);
            var ctx = await ObterContextoNfseAsync(config.Cnpj, ct);
            var openAc = _openAcFactory.Criar(config, ctx);
            var homologacao = !EhProducao(config.Ambiente);

            // NT 008: preferir PDF local a partir do XML (API ADN de DANFSe suspensa).
            string? xmlNfse = null;
            try
            {
                var consulta = await SefazRetry.ExecuteAsync(_fiscalConfig, _logger, "NFSeConsultaChaveDanfse", () =>
                    openAc.ConsultaChaveAsync(chaveAcesso));
                if (consulta.Resultado?.StatusProcessamento == StatusProcessamentoDistribuicao.DOCUMENTOS_LOCALIZADOS)
                {
                    xmlNfse = consulta.Resultado.Lote?
                        .FirstOrDefault(d => string.Equals(d.ChaveAcesso, chaveAcesso, StringComparison.Ordinal))
                        ?.ArquivoXml;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Consulta ADN para DANFSe local falhou; tentando fallback. Chave={Chave}", chaveAcesso);
            }

            var pdfLocal = _danfseLocal.TentarGerarDeXml(xmlNfse, homologacao);
            if (pdfLocal is { Length: > 0 })
            {
                return FiscalResponse.Ok(chaveAcesso, string.Empty, "100", "DANFSe gerado localmente (NT 008).",
                    xml: xmlNfse, pdf: Convert.ToBase64String(pdfLocal));
            }

            try
            {
                var pdfAdn = await SefazRetry.ExecuteAsync(_fiscalConfig, _logger, "NFSeDownloadDanfse", () =>
                    openAc.DownloadDANFSeAsync(chaveAcesso));
                if (pdfAdn is { Length: > 0 })
                {
                    return FiscalResponse.Ok(chaveAcesso, string.Empty, "100", "DANFSe obtido via ADN.",
                        xml: xmlNfse, pdf: Convert.ToBase64String(pdfAdn));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Download DANFSe via ADN indisponível. Chave={Chave}", chaveAcesso);
            }

            return FiscalResponse.Falha(
                "DanfseIndisponivel",
                string.IsNullOrWhiteSpace(xmlNfse)
                    ? "DANFSe indisponível: XML da NFS-e ainda não localizado no ADN (aguarde indexação) e API oficial de PDF suspensa (NT 008)."
                    : "DANFSe indisponível: falha ao gerar PDF local a partir do XML e API ADN sem retorno.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao baixar DANFSe Chave={Chave}", chaveAcesso);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    private async Task<string?> ObterDanfseBase64AposEmissaoAsync(
        OpenNFSeNacional openAc,
        string chave,
        NotaFiscalServico? nota,
        string? xmlAutorizado,
        bool homologacao)
    {
        try
        {
            var pdf = _danfseLocal.TentarGerar(nota, homologacao)
                      ?? _danfseLocal.TentarGerarDeXml(xmlAutorizado, homologacao);

            if ((pdf is null || pdf.Length == 0) && !string.IsNullOrWhiteSpace(chave))
            {
                try
                {
                    pdf = await openAc.DownloadDANFSeAsync(chave);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fallback ADN DANFSe após emissão indisponível. Chave={Chave}", chave);
                }
            }

            if (pdf is { Length: > 0 })
                return Convert.ToBase64String(pdf);

            _logger.LogWarning("DANFSe não gerado após emissão; prosseguindo sem PDF. Chave={Chave}", chave);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DANFSe não obtido após emissão; prosseguindo sem PDF. Chave={Chave}", chave);
        }

        return null;
    }

    private static bool EhProducao(string? ambiente) =>
        string.Equals(ambiente, "Producao", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ambiente, "Produção", StringComparison.OrdinalIgnoreCase);

    private async Task<EmitenteNfseContexto> ObterContextoNfseAsync(string cnpj, CancellationToken ct)
    {
        var entidade = await _db.Emitentes.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Cnpj == cnpj && e.Ativo, ct);

        return new EmitenteNfseContexto
        {
            InscricaoMunicipal = entidade?.InscricaoMunicipal,
            Email = entidade?.Email,
            CodigoMunicipioIbge = entidade?.CodigoMunicipio
        };
    }

    private static void ValidarPrecondicoesEmitente(
        ConfiguracaoEmitenteRequest emitente,
        EmitenteNfseContexto ctx,
        NFSeEmitirRequest request)
    {
        var codMun = ctx.CodigoMunicipioIbge ?? emitente.Endereco?.CodigoMunicipio;
        if (string.IsNullOrWhiteSpace(codMun))
            throw new ArgumentException("Emitente sem codigoMunicipio — obrigatório para NFS-e.");

        var email = request.Prestador?.Email ?? ctx.Email;
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("E-mail do prestador é obrigatório (cadastro emitente ou request.prestador.email).");
    }

    private async Task RegistrarLogAsync(
        string cnpj, string serie, int numero, string chave, string protocolo,
        string status, string codigo, string mensagem, string ambiente, CancellationToken ct)
    {
        try
        {
            _db.EmissaoLogs.Add(new EmissaoLog
            {
                Cnpj = cnpj,
                Modelo = NFSeDpsMapper.ModeloNumeracao,
                Serie = serie,
                Numero = numero,
                ChaveAcesso = chave,
                Protocolo = protocolo,
                Status = status,
                CodigoStatus = codigo,
                MensagemStatus = mensagem.Length > 500 ? mensagem[..500] : mensagem,
                Ambiente = ambiente,
                DataEmissao = DateTime.UtcNow,
                DataProcessamento = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar log NFS-e (não bloqueia resposta).");
        }
    }

    private async Task AtualizarLogCanceladoAsync(
        string chave, string protocolo, string mensagem, CancellationToken ct)
    {
        try
        {
            var log = await _db.EmissaoLogs.FirstOrDefaultAsync(e => e.ChaveAcesso == chave, ct);
            if (log is null) return;

            log.Status = "Cancelado";
            log.Protocolo = protocolo;
            log.CodigoStatus = "135";
            log.MensagemStatus = mensagem.Length > 500 ? mensagem[..500] : mensagem;
            log.DataProcessamento = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao atualizar log de cancelamento NFS-e.");
        }
    }

    private async Task SincronizarNumeracaoAsync(string cnpj, string serie, int numero, string ambiente, CancellationToken ct)
    {
        try
        {
            await _numeracaoService.ConfirmarNumeroAsync(
                cnpj, NFSeDpsMapper.ModeloNumeracao, serie, numero, ambiente, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao sincronizar numeração NFS-e CNPJ={CNPJ} Serie={Serie} Ambiente={Ambiente} Numero={Numero}",
                cnpj, serie, ambiente, numero);
        }
    }

    private static string ClassificarExcecao(Exception ex) => ex switch
    {
        ArgumentException => "Validacao",
        KeyNotFoundException => "EmitenteNaoEncontrado",
        InvalidOperationException => "OperacaoInvalida",
        _ => "ErroInterno"
    };
}
