using DFe.Classes.Entidades;
using DFe.Classes.Flags;
using DFe.Utils;
using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.Helpers;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
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
using NFe.Classes.Informacoes.Pagamento;
using NFe.Classes.Informacoes.Total;
using NFe.Classes.Informacoes.Transporte;
using NFe.Classes.Servicos.Tipos;
using NFe.Servicos;
using NFe.Utils;

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
    private readonly ILogger<NFCeService> _logger;

    public NFCeService(
        FiscalConfig globalConfig,
        AppDbContext db,
        DanfeService danfeService,
        ILogger<NFCeService> logger)
    {
        _globalConfig = globalConfig;
        _db = db;
        _danfeService = danfeService;
        _logger = logger;
    }

    public async Task<FiscalResponse> EmitirAsync(NFCeEmitirRequest request, CancellationToken ct = default)
    {
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente);
            var nfce = ConstruirNFCe(request, config);

            using var servicos = new ServicosNFe(config);
            var idLote = (int)(Math.Abs(DateTime.UtcNow.Ticks) % int.MaxValue);
            var retorno = servicos.NFeAutorizacao(idLote, IndicadorSincronizacao.Sincrono,
                new List<NFe.Classes.NFe> { nfce });

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
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente);

            using var servicos = new ServicosNFe(config);
            var idLote = (int)(Math.Abs(DateTime.UtcNow.Ticks) % int.MaxValue);
            var retorno = servicos.RecepcaoEventoCancelamento(
                idLote, 1,
                request.Protocolo, request.ChaveAcesso,
                request.Justificativa,
                request.ConfiguracaoEmitente.Cnpj);

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

        var itens = req.Itens.Select((item, idx) => ConstruirItem(item, idx + 1)).ToList();
        var totalProdutos = req.Itens.Sum(i => i.ValorTotalBruto);
        var totalDesconto = req.Itens.Sum(i => i.ValorDesconto ?? 0);
        var totalNota = totalProdutos - totalDesconto;

        var troco = req.Pagamentos.Sum(p => p.ValorPagamento) - totalNota;

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
                    ICMSTot = new ICMSTot
                    {
                        vBC = req.Itens.Sum(i => i.BaseCalculoIcms ?? 0),
                        vICMS = req.Itens.Sum(i => i.ValorIcms ?? 0),
                        vICMSDeson = 0,
                        vFCP = 0,
                        vBCST = 0,
                        vST = 0,
                        vFCPST = 0,
                        vFCPSTRet = 0,
                        vProd = totalProdutos,
                        vFrete = 0,
                        vSeg = 0,
                        vDesc = totalDesconto,
                        vII = 0,
                        vIPI = 0,
                        vIPIDevol = 0,
                        vPIS = req.Itens.Sum(i => i.ValorPis ?? 0),
                        vCOFINS = req.Itens.Sum(i => i.ValorCofins ?? 0),
                        vOutro = 0,
                        vNF = totalNota,
                        vTotTrib = 0
                    }
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
                        vTroco = troco > 0 ? troco : (decimal?)null
                    }
                }
            },
            infNFeSupl = new infNFeSupl
            {
                qrCode = string.Empty,
                urlChave = string.Empty
            }
        };
    }

    private static det ConstruirItem(ItemNFeRequest item, int numero)
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
            imposto = new imposto
            {
                ICMS = new ICMS
                {
                    TipoICMS = new ICMS00
                    {
                        orig = (OrigemMercadoria)int.Parse(item.OrigemMercadoria ?? "0"),
                        CST = Csticms.Cst00,
                        modBC = DeterminacaoBaseIcms.DbiValorOperacao,
                        vBC = item.BaseCalculoIcms ?? 0,
                        pICMS = item.AliquotaIcms ?? 0,
                        vICMS = item.ValorIcms ?? 0
                    }
                },
                PIS = new PIS
                {
                    TipoPIS = new PISAliq
                    {
                        CST = (CSTPIS)int.Parse(item.CstPis ?? "07"),
                        vBC = item.BaseCalculoPis ?? 0,
                        pPIS = item.AliquotaPis ?? 0,
                        vPIS = item.ValorPis ?? 0
                    }
                },
                COFINS = new COFINS
                {
                    TipoCOFINS = new COFINSAliq
                    {
                        CST = (CSTCOFINS)int.Parse(item.CstCofins ?? "07"),
                        vBC = item.BaseCalculoCofins ?? 0,
                        pCOFINS = item.AliquotaCofins ?? 0,
                        vCOFINS = item.ValorCofins ?? 0
                    }
                }
            }
        };
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
        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("certificado") || msg.Contains("pfx") || msg.Contains("senha")) return "CertificadoInvalido";
        if (msg.Contains("timeout") || msg.Contains("unavailable") || msg.Contains("connection")) return "ServicoIndisponivel";
        if (msg.Contains("schema") || msg.Contains("xml")) return "ValidacaoSchema";
        return "ErroInterno";
    }
}
