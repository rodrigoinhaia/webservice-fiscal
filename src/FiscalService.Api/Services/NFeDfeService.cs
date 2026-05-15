using DFe.Classes.Entidades;
using DFe.Classes.Flags;
using DFe.Utils;
using FiscalService.Api.Config;
using FiscalService.Api.Helpers;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services.Fiscal;
using NFe.Classes.Servicos.DistribuicaoDFe;
using NFe.Classes.Servicos.Tipos;
using NFe.Servicos;
using NFe.Utils;
using NFe.Utils.DistribuicaoDFe;

namespace FiscalService.Api.Services;

/// <summary>Distribuição DF-e e manifestação do destinatário (SEFAZ nacional).</summary>
public class NFeDfeService
{
    private readonly FiscalConfig _globalConfig;
    private readonly EmitenteService _emitenteService;
    private readonly ILogger<NFeDfeService> _logger;

    public NFeDfeService(
        FiscalConfig globalConfig,
        EmitenteService emitenteService,
        ILogger<NFeDfeService> logger)
    {
        _globalConfig = globalConfig;
        _emitenteService = emitenteService;
        _logger = logger;
    }

    public async Task<DistribuicaoDfeResponse> DistribuirAsync(
        NFeDistribuicaoDfeRequest request,
        CancellationToken ct = default)
    {
        try
        {
            request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
            var emitente = request.ConfiguracaoEmitente!;
            var documento = SomenteDigitos(request.DocumentoInteressado ?? emitente.Cnpj);
            var ufAutor = emitente.Uf;
            var ultNsu = request.UltNsu ?? "0";
            var nsu = request.Nsu ?? "0";
            var chave = request.ChaveAcesso ?? string.Empty;

            var config = ConstruirConfiguracao(emitente, descompactar: true);

            using var servicos = new ServicosNFe(config);
            var retorno = SefazRetry.Execute(_globalConfig, _logger, "NFeDistribuicaoDFe", () =>
                servicos.NfeDistDFeInteresse(ufAutor, documento, ultNsu, nsu, chave));

            var ret = retorno.Retorno;
            if (ret is null)
                return FalhaDistribuicao("ErroInterno", "Retorno da distribuição DF-e não pôde ser processado.");

            var cStat = ret.cStat.ToString();
            var xMotivo = ret.xMotivo ?? string.Empty;
            var sucesso = ret.cStat is 137 or 138 or 656;

            var documentos = new List<DocumentoDistribuicaoDto>();
            if (ret.loteDistDFeInt is not null)
            {
                foreach (var doc in ret.loteDistDFeInt)
                {
                    var xml = string.Empty;
                    if (doc.XmlNfe is { Length: > 0 })
                    {
                        try
                        {
                            xml = config.UnZip
                                ? Compressao.Unzip(doc.XmlNfe)
                                : Convert.ToBase64String(doc.XmlNfe);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Falha ao descompactar documento NSU={Nsu}", doc.NSU);
                            xml = Convert.ToBase64String(doc.XmlNfe);
                        }
                    }

                    documentos.Add(new DocumentoDistribuicaoDto
                    {
                        Nsu = doc.NSU.ToString(),
                        Schema = doc.schema ?? string.Empty,
                        Xml = xml
                    });
                }
            }

            _logger.LogInformation(
                "Distribuição DF-e: cStat={CStat} docs={Qtd} ultNSU={Ult}",
                cStat, documentos.Count, ret.ultNSU);

            return new DistribuicaoDfeResponse
            {
                Sucesso = sucesso,
                CodigoStatus = cStat,
                Mensagem = xMotivo,
                UltNsu = ret.ultNSU.ToString(),
                MaxNsu = ret.maxNSU.ToString(),
                Documentos = documentos,
                XmlRetorno = retorno.RetornoStr
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na distribuição DF-e");
            return FalhaDistribuicao(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> ManifestarDestinatarioAsync(
        NFeManifestarDestinatarioRequest request,
        CancellationToken ct = default)
    {
        try
        {
            request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
            var emitente = request.ConfiguracaoEmitente!;
            var tipoEvento = ManifestacaoDestinatarioMapper.Resolver(request.TipoManifestacao);

            if (ManifestacaoDestinatarioMapper.ExigeJustificativa(tipoEvento)
                && (string.IsNullOrWhiteSpace(request.Justificativa) || request.Justificativa.Trim().Length < 15))
            {
                return FiscalResponse.Falha("Validacao",
                    "Justificativa com no mínimo 15 caracteres é obrigatória para Operação Não Realizada (210240).");
            }

            var documento = SomenteDigitos(request.DocumentoManifestante ?? emitente.Cnpj);
            var config = ConstruirConfiguracao(emitente);

            using var servicos = new ServicosNFe(config);
            var idLote = (int)(Math.Abs(DateTime.UtcNow.Ticks) % int.MaxValue);
            var retorno = SefazRetry.Execute(_globalConfig, _logger, "ManifestacaoDestinatario", () =>
                servicos.RecepcaoEventoManifestacaoDestinatario(
                    idLote,
                    request.SequenciaEvento,
                    request.ChaveAcesso,
                    tipoEvento,
                    documento,
                    request.Justificativa));

            var retEvento = retorno.Retorno?.retEvento?.FirstOrDefault()?.infEvento;
            if (retEvento is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno da manifestação não pôde ser processado.",
                    xmlRetorno: retorno.RetornoStr);

            var cStat = retEvento.cStat.ToString();
            var xMotivo = retEvento.xMotivo ?? string.Empty;
            var protocolo = retEvento.nProt ?? string.Empty;
            var sucesso = retEvento.cStat is 135 or 136 or 155;

            if (!sucesso)
                return FiscalResponse.Falha("RejeicaoSefaz", $"Manifestação rejeitada: {xMotivo}", $"cStat: {cStat}",
                    xmlRetorno: retorno.RetornoStr);

            _logger.LogInformation("Manifestação registrada: Chave={Chave} Tipo={Tipo} Protocolo={Protocolo}",
                request.ChaveAcesso, tipoEvento, protocolo);

            return new FiscalResponse
            {
                Sucesso = true,
                ChaveAcesso = request.ChaveAcesso,
                Protocolo = protocolo,
                CodigoStatus = cStat,
                Mensagem = xMotivo,
                XmlRetorno = retorno.RetornoStr
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao manifestar destinatário: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    private ConfiguracaoServico ConstruirConfiguracao(ConfiguracaoEmitenteRequest emitente, bool descompactar = false)
    {
        var config = new ConfiguracaoServico
        {
            cUF = UfHelper.MapearUf(emitente.Uf),
            tpAmb = emitente.Ambiente == "Producao" ? TipoAmbiente.Producao : TipoAmbiente.Homologacao,
            ModeloDocumento = ModeloDocumento.NFe,
            DiretorioSchemas = _globalConfig.DiretorioSchemas,
            SalvarXmlServicos = _globalConfig.SalvarXmls,
            DiretorioSalvarXml = _globalConfig.DiretorioXmls,
            TimeOut = _globalConfig.TimeoutWs,
            UnZip = descompactar
        };

        var certPath = _globalConfig.ResolveCertificadoPath(emitente.CertificadoPath);
        config.Certificado.TipoCertificado = TipoCertificado.A1Arquivo;
        config.Certificado.Arquivo = certPath;
        config.Certificado.Senha = emitente.CertificadoSenha;

        return config;
    }

    private static string SomenteDigitos(string valor) =>
        new string(valor.Where(char.IsDigit).ToArray());

    private static DistribuicaoDfeResponse FalhaDistribuicao(string tipo, string mensagem, string? detalhe = null) =>
        new()
        {
            Sucesso = false,
            Erro = new ErroResponse
            {
                Tipo = tipo,
                Mensagem = mensagem,
                Detalhe = detalhe,
                Timestamp = DateTime.UtcNow
            }
        };

    private static string ClassificarExcecao(Exception ex)
    {
        if (ex is ArgumentException)
            return "Validacao";
        if (ex is KeyNotFoundException)
            return "EmitenteNaoEncontrado";

        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("certificado") || msg.Contains("pfx") || msg.Contains("senha"))
            return "CertificadoInvalido";
        if (msg.Contains("timeout") || msg.Contains("unavailable") || msg.Contains("connection"))
            return "ServicoIndisponivel";
        return "ErroInterno";
    }
}
