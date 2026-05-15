using DFe.Classes.Entidades;
using DFe.Classes.Flags;
using DFe.Utils;
using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.Helpers;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services.Fiscal;
using NFe.Classes;
using NFe.Classes.Informacoes;
using NFe.Classes.Informacoes.Detalhe;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal.Tipos;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;
using NFe.Classes.Informacoes.Emitente;
using NFe.Classes.Informacoes.Identificacao;
using NFe.Classes.Informacoes.Identificacao.Tipos;
using NFe.Classes.Informacoes.Observacoes;
using NFe.Classes.Informacoes.Pagamento;
using NFe.Classes.Informacoes.Total;
using NFe.Classes.Informacoes.Transporte;
using NFe.Classes.Servicos.Tipos;
using NFe.Servicos;
using NFe.Utils;
using NFe.Utils.InformacoesSuplementares;
using NFe.Utils.NFe;

namespace FiscalService.Api.Services;

/// <summary>
/// Orquestra emissão e cancelamento de NFC-e 4.0.
/// ATENÇÃO: Não usar como Singleton — DFe.NET não é thread-safe.
/// </summary>
public class NFCeService
{
    private readonly FiscalConfig _globalConfig;
    private readonly AppDbContext _db;
    private readonly DanfeService _danfeService;
    private readonly NumeracaoService _numeracaoService;
    private readonly EmitenteService _emitenteService;
    private readonly ILogger<NFCeService> _logger;

    public NFCeService(
        FiscalConfig globalConfig,
        AppDbContext db,
        DanfeService danfeService,
        NumeracaoService numeracaoService,
        EmitenteService emitenteService,
        ILogger<NFCeService> logger)
    {
        _globalConfig = globalConfig;
        _db = db;
        _danfeService = danfeService;
        _numeracaoService = numeracaoService;
        _emitenteService = emitenteService;
        _logger = logger;
    }

    public async Task<FiscalResponse> EmitirAsync(NFCeEmitirRequest request, CancellationToken ct = default)
    {
        try
        {
            request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
            ImpostoTributacaoCatalog.ValidarItensOuLancar(request.ConfiguracaoEmitente.Crt, request.Itens);
            NFeTotaisCalculator.ValidarConsistenciaOuLancar(request.Itens);

            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente);
            var nfce = ConstruirNFCe(request, config);
            nfce.Assina(config);
            PreencherInformacoesSuplementaresNfce(nfce, config, request);

            using var servicos = new ServicosNFe(config);
            var idLote = (int)(Math.Abs(DateTime.UtcNow.Ticks) % int.MaxValue);
            var retorno = SefazRetry.Execute(_globalConfig, _logger, "NFCeAutorizacao", () =>
                servicos.NFeAutorizacao(idLote, IndicadorSincronizacao.Sincrono,
                    new List<NFe.Classes.NFe> { nfce }));

            var infProt = retorno.Retorno?.protNFe?.infProt;
            if (infProt is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno da SEFAZ não pôde ser processado.");

            var cStat = infProt.cStat.ToString();
            var xMotivo = infProt.xMotivo ?? string.Empty;
            var chave = infProt.chNFe ?? string.Empty;
            var protocolo = infProt.nProt ?? string.Empty;
            var autorizado = infProt.cStat == 100;

            if (!autorizado)
                return FiscalResponse.Falha("RejeicaoSefaz", $"Rejeição SEFAZ: {xMotivo}", $"cStat: {cStat}");

            var xmlAutorizado = retorno.RetornoStr ?? string.Empty;

            await RegistrarLogAsync(request.ConfiguracaoEmitente.Cnpj, "65", request.Serie,
                request.NumeroNota, chave, protocolo, "Autorizado", cStat, xMotivo,
                request.ConfiguracaoEmitente.Ambiente, ct);

            await SincronizarNumeracaoAsync(request.ConfiguracaoEmitente.Cnpj, "65",
                request.Serie, request.NumeroNota, ct);

            string? pdfBase64 = null;
            if (!string.IsNullOrWhiteSpace(xmlAutorizado))
            {
                try { pdfBase64 = _danfeService.GerarNFCePdf(xmlAutorizado, request.IdCsc, request.Csc); }
                catch (Exception ex) { _logger.LogWarning(ex, "DANFE NFC-e não gerado."); }
            }

            _logger.LogInformation("NFC-e autorizada: Chave={Chave}", chave);
            return FiscalResponse.Ok(chave, protocolo, cStat, xMotivo, xmlAutorizado, pdfBase64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao emitir NFC-e");
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> CancelarAsync(NFeCancelarRequest request, CancellationToken ct = default)
    {
        request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
        return CancelarCore(request);
    }

    /// <summary>Consulta status do serviço SEFAZ NFC-e (modelo 65) usando <see cref="ServicosNFe"/>.</summary>
    public StatusServicoResponse ConsultarStatusSefaz(ConfiguracaoEmitenteRequest emitente)
    {
        try
        {
            var config = ConstruirConfiguracao(emitente);
            using var servicos = new ServicosNFe(config);
            var retorno = SefazRetry.Execute(_globalConfig, _logger, "NFCeStatusServico", () =>
                servicos.NfeStatusServico());

            var ret = retorno.Retorno;
            if (ret is null)
                return new StatusServicoResponse { Sucesso = false, Mensagem = "Sem retorno da SEFAZ." };

            return new StatusServicoResponse
            {
                Sucesso = ret.cStat == 107,
                CodigoStatus = ret.cStat.ToString(),
                Mensagem = ret.xMotivo,
                Uf = emitente.Uf,
                Modelo = "NFCe",
                Ambiente = emitente.Ambiente,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status SEFAZ NFC-e");
            return new StatusServicoResponse
            {
                Sucesso = false,
                Mensagem = ex.Message,
                Erro = new ErroResponse { Tipo = ClassificarExcecao(ex), Mensagem = ex.Message, Timestamp = DateTime.UtcNow }
            };
        }
    }

    private FiscalResponse CancelarCore(NFeCancelarRequest request)
    {
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente);

            using var servicos = new ServicosNFe(config);
            var idLote = (int)(Math.Abs(DateTime.UtcNow.Ticks) % int.MaxValue);
            var retorno = SefazRetry.Execute(_globalConfig, _logger, "NFCeCancelamento", () =>
                servicos.RecepcaoEventoCancelamento(
                    idLote, 1,
                    request.Protocolo, request.ChaveAcesso,
                    request.Justificativa,
                    request.ConfiguracaoEmitente!.Cnpj));

            var retEvento = retorno.Retorno?.retEvento?.FirstOrDefault()?.infEvento;
            if (retEvento is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno do cancelamento não pôde ser processado.");

            var cStat = retEvento.cStat.ToString();
            var xMotivo = retEvento.xMotivo ?? string.Empty;
            var protocolo = retEvento.nProt ?? string.Empty;
            var sucesso = retEvento.cStat == 135;

            if (!sucesso)
                return FiscalResponse.Falha("RejeicaoSefaz", $"Cancelamento NFC-e rejeitado: {xMotivo}", $"cStat: {cStat}");

            _logger.LogInformation("NFC-e cancelada: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Ok(request.ChaveAcesso, protocolo, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar NFC-e");
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private ConfiguracaoServico ConstruirConfiguracao(ConfiguracaoEmitenteRequest emitente)
    {
        var config = new ConfiguracaoServico
        {
            cUF = UfHelper.MapearUf(emitente.Uf),
            tpAmb = emitente.Ambiente == "Producao" ? TipoAmbiente.Producao : TipoAmbiente.Homologacao,
            tpEmis = TipoEmissao.teNormal,
            ModeloDocumento = ModeloDocumento.NFCe,
            DiretorioSchemas = _globalConfig.DiretorioSchemas,
            SalvarXmlServicos = _globalConfig.SalvarXmls,
            DiretorioSalvarXml = _globalConfig.DiretorioXmls,
            TimeOut = _globalConfig.TimeoutWs
        };

        var certPath = _globalConfig.ResolveCertificadoPath(emitente.CertificadoPath);
        config.Certificado.TipoCertificado = TipoCertificado.A1Arquivo;
        config.Certificado.Arquivo = certPath;
        config.Certificado.Senha = emitente.CertificadoSenha;

        return config;
    }

    private static NFe.Classes.NFe ConstruirNFCe(NFCeEmitirRequest req, ConfiguracaoServico config)
    {
        var emitente = req.ConfiguracaoEmitente;
        var uf = UfHelper.MapearUf(emitente.Uf);
        var dhEmissao = DateTimeOffset.Now;

        var itens = req.Itens.Select((item, idx) => ConstruirItem(item, idx + 1, req.ConfiguracaoEmitente.Crt)).ToList();
        var totais = NFeTotaisCalculator.Calcular(req.Itens);
        var troco = req.Pagamentos.Sum(p => p.ValorPagamento) - totais.ValorNota;

        return new NFe.Classes.NFe
        {
            infNFe = new infNFe
            {
                versao = "4.00",
                ide = new ide
                {
                    cUF = uf,
                    natOp = req.NaturezaOperacao,
                    mod = ModeloDocumento.NFCe,
                    serie = int.Parse(req.Serie),
                    nNF = req.NumeroNota,
                    dhEmi = dhEmissao,
                    tpNF = TipoNFe.tnSaida,
                    idDest = DestinoOperacao.doInterna,
                    cMunFG = long.TryParse(emitente.Endereco?.CodigoMunicipio, out var cMunFG) ? cMunFG : 0,
                    tpImp = TipoImpressao.tiNFCe,
                    tpEmis = TipoEmissao.teNormal,
                    tpAmb = config.tpAmb,
                    finNFe = FinalidadeNFe.fnNormal,
                    indFinal = ConsumidorFinal.cfConsumidorFinal,
                    indPres = PresencaComprador.pcPresencial,
                    procEmi = ProcessoEmissao.peAplicativoContribuinte,
                    verProc = "1.0"
                },
                emit = new emit
                {
                    CNPJ = emitente.Cnpj,
                    xNome = emitente.RazaoSocial,
                    xFant = emitente.NomeFantasia,
                    IE = emitente.Ie,
                    CRT = (CRT)emitente.Crt,
                    enderEmit = emitente.Endereco is null ? null : new enderEmit
                    {
                        xLgr = emitente.Endereco.Logradouro ?? string.Empty,
                        nro = emitente.Endereco.Numero ?? string.Empty,
                        xCpl = emitente.Endereco.Complemento,
                        xBairro = emitente.Endereco.Bairro ?? string.Empty,
                        cMun = long.TryParse(emitente.Endereco.CodigoMunicipio, out var cMunEmit) ? cMunEmit : 0,
                        xMun = emitente.Endereco.Municipio ?? string.Empty,
                        UF = uf,
                        CEP = emitente.Endereco.Cep ?? string.Empty,
                        cPais = 1058,
                        xPais = "Brasil",
                        fone = long.TryParse(emitente.Endereco.Telefone, out var foneEmit) ? foneEmit : (long?)null
                    }
                },
                det = itens,
                total = new total
                {
                    ICMSTot = NFeTotaisCalculator.MontarIcmsTot(totais)
                },
                transp = new transp { modFrete = ModalidadeFrete.mfSemFrete },
                pag = new List<pag>
                {
                    new pag
                    {
                        detPag = req.Pagamentos.Select(p => new detPag
                        {
                            tPag = (FormaPagamento)int.Parse(p.FormaPagamento),
                            vPag = p.ValorPagamento
                        }).ToList(),
                        vTroco = troco > 0 ? troco : null
                    }
                },
                infAdic = string.IsNullOrWhiteSpace(req.InformacoesAdicionais) ? null : new infAdic
                {
                    infCpl = req.InformacoesAdicionais
                }
            },
            infNFeSupl = new infNFeSupl { qrCode = string.Empty, urlChave = string.Empty }
        };
    }

    /// <summary>Preenche QR Code e URL de consulta pública (DFe.NET — <see cref="ExtinfNFeSupl"/>).</summary>
    private static void PreencherInformacoesSuplementaresNfce(
        NFe.Classes.NFe nfce, ConfiguracaoServico config, NFCeEmitirRequest req)
    {
        if (nfce.infNFeSupl is null)
            nfce.infNFeSupl = new infNFeSupl();

        var versaoQr = ParseVersaoQrCode(req.QrCodeVersao);
        var certificadoCfg = versaoQr == VersaoQrCode.QrCodeVersao3 ? config.Certificado : null;

        nfce.infNFeSupl.qrCode = nfce.infNFeSupl.ObterUrlQrCode(
            nfce, versaoQr, req.IdCsc.Trim(), req.Csc.Trim(), certificadoCfg);

        nfce.infNFeSupl.urlChave = nfce.infNFeSupl.ObterUrlConsulta(nfce, versaoQr);
    }

    private static VersaoQrCode ParseVersaoQrCode(string? valor) =>
        valor?.Trim() switch
        {
            "1" => VersaoQrCode.QrCodeVersao1,
            "3" => VersaoQrCode.QrCodeVersao3,
            _ => VersaoQrCode.QrCodeVersao2
        };

    private static det ConstruirItem(ItemNFeRequest item, int numero, int crt)
    {
        return new det
        {
            nItem = numero,
            prod = new prod
            {
                cProd = item.CodigoProduto,
                cEAN = item.CodigoEan ?? "SEM GTIN",
                xProd = item.DescricaoProduto,
                NCM = item.Ncm ?? "00000000",
                CFOP = int.Parse(item.Cfop ?? "5102"),
                uCom = item.UnidadeComercial,
                qCom = item.QuantidadeComercial,
                vUnCom = item.ValorUnitarioComercial,
                vProd = item.ValorTotalBruto,
                cEANTrib = item.CodigoEan ?? "SEM GTIN",
                uTrib = item.UnidadeTributavel ?? item.UnidadeComercial,
                qTrib = item.QuantidadeTributavel ?? item.QuantidadeComercial,
                vUnTrib = item.ValorUnitarioTributavel ?? item.ValorUnitarioComercial,
                vDesc = item.ValorDesconto,
                indTot = IndicadorTotal.ValorDoItemCompoeTotalNF
            },
            imposto = ImpostoItemFactory.Criar(item, crt)
        };
    }

    private async Task SincronizarNumeracaoAsync(string cnpj, string modelo, string serie, int numero, CancellationToken ct)
    {
        try
        {
            await _numeracaoService.ConfirmarNumeroAsync(cnpj, modelo, serie, numero, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao sincronizar numeração NFC-e: CNPJ={CNPJ} Modelo={Modelo} Serie={Serie} Numero={Numero}",
                cnpj, modelo, serie, numero);
        }
    }

    private async Task RegistrarLogAsync(string cnpj, string modelo, string serie, int numero,
        string chave, string protocolo, string status, string cStat, string mensagem,
        string ambiente, CancellationToken ct)
    {
        try
        {
            _db.EmissaoLogs.Add(new EmissaoLog
            {
                Cnpj = cnpj,
                Modelo = modelo,
                Serie = serie,
                Numero = numero,
                ChaveAcesso = chave,
                Protocolo = protocolo,
                Status = status,
                CodigoStatus = cStat,
                MensagemStatus = mensagem,
                Ambiente = ambiente,
                DataEmissao = DateTime.UtcNow,
                DataProcessamento = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao salvar log de emissão NFC-e.");
        }
    }

    private static string ClassificarExcecao(Exception ex)
    {
        if (ex is TributacaoNaoSuportadaException)
            return "TributacaoInvalida";
        if (ex is KeyNotFoundException)
            return "EmitenteNaoEncontrado";
        if (ex is ArgumentException)
            return "Validacao";

        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("certificado") || msg.Contains("pfx") || msg.Contains("senha")) return "CertificadoInvalido";
        if (msg.Contains("timeout") || msg.Contains("unavailable") || msg.Contains("connection")) return "ServicoIndisponivel";
        if (msg.Contains("schema") || msg.Contains("xml")) return "ValidacaoSchema";
        return "ErroInterno";
    }
}
