using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services.Fiscal;
using Microsoft.EntityFrameworkCore;
using OpenAC.Net.NFSe.Nacional;
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
    private readonly ILogger<NFSeService> _logger;

    public NFSeService(
        FiscalConfig fiscalConfig,
        AppDbContext db,
        NumeracaoService numeracaoService,
        EmitenteService emitenteService,
        NFSeOpenAcFactory openAcFactory,
        ILogger<NFSeService> logger)
    {
        _fiscalConfig = fiscalConfig;
        _db = db;
        _numeracaoService = numeracaoService;
        _emitenteService = emitenteService;
        _openAcFactory = openAcFactory;
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

            NFSeDpsXmlNormalizer.PrepararDpsAntesAssinatura(dps);
            dps.Assinar(openAc.Configuracoes);
            NFSeDpsXmlNormalizer.NormalizarDpsAposAssinatura(dps);

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

            string? pdfBase64 = null;
            if (!string.IsNullOrWhiteSpace(chave))
            {
                try
                {
                    var pdf = await openAc.DownloadDANFSeAsync(chave);
                    if (pdf is { Length: > 0 })
                        pdfBase64 = Convert.ToBase64String(pdf);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DANFSe não obtido após emissão; prosseguindo sem PDF.");
                }
            }

            _logger.LogInformation("NFS-e autorizada: Chave={Chave}", chave);
            return FiscalResponse.Ok(chave, protocolo, "100", mensagem,
                xml: retorno.XmlRetorno, pdf: pdfBase64);
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
            NFSeDpsXmlNormalizer.PrepararEventoAntesAssinatura(evento);
            evento.Assinar(openAc.Configuracoes);
            NFSeDpsXmlNormalizer.NormalizarEventoAposAssinatura(evento);

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

            if (!retorno.Sucesso)
            {
                var erros = NFSeOpenAcResponseHelper.FormatarErros(retorno.Resultado?.Erros);
                return FiscalResponse.Falha("ConsultaNfseNacional", erros,
                    xmlRetorno: retorno.XmlRetorno);
            }

            var resultado = retorno.Resultado!;
            var situacao = resultado.StatusProcessamento.ToString();
            var doc = resultado.Lote?.FirstOrDefault(d =>
                string.Equals(d.ChaveAcesso, request.ChaveAcesso, StringComparison.Ordinal));
            return FiscalResponse.Ok(
                doc?.ChaveAcesso ?? request.ChaveAcesso,
                doc?.NSU.ToString() ?? string.Empty,
                situacao,
                NFSeOpenAcResponseHelper.FormatarSucesso(situacao, resultado.Alertas),
                xml: doc?.ArquivoXml);
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

            var pdf = await SefazRetry.ExecuteAsync(_fiscalConfig, _logger, "NFSeDownloadDanfse", () =>
                openAc.DownloadDANFSeAsync(chaveAcesso));

            if (pdf is null or { Length: 0 })
                return FiscalResponse.Falha("DanfseIndisponivel", "DANFSe não retornado pelo ADN.");

            return FiscalResponse.Ok(chaveAcesso, string.Empty, "100", "DANFSe obtido.",
                pdf: Convert.ToBase64String(pdf));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao baixar DANFSe Chave={Chave}", chaveAcesso);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

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
