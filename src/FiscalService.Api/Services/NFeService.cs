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
using Microsoft.EntityFrameworkCore;
using NFe.Classes;
using NFe.Classes.Informacoes;
using NFe.Classes.Informacoes.Detalhe;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal.Tipos;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;
using NFe.Classes.Informacoes.Destinatario;
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

namespace FiscalService.Api.Services;

/// <summary>
/// Orquestra emissão, cancelamento, CC-e, consulta e inutilização de NF-e 4.0.
/// ATENÇÃO: Não usar como Singleton — DFe.NET não é thread-safe.
/// </summary>
public class NFeService
{
    private readonly FiscalConfig _globalConfig;
    private readonly AppDbContext _db;
    private readonly DanfeService _danfeService;
    private readonly NumeracaoService _numeracaoService;
    private readonly EmitenteService _emitenteService;
    private readonly ILogger<NFeService> _logger;

    public NFeService(
        FiscalConfig globalConfig,
        AppDbContext db,
        DanfeService danfeService,
        NumeracaoService numeracaoService,
        EmitenteService emitenteService,
        ILogger<NFeService> logger)
    {
        _globalConfig = globalConfig;
        _db = db;
        _danfeService = danfeService;
        _numeracaoService = numeracaoService;
        _emitenteService = emitenteService;
        _logger = logger;
    }

    public async Task<FiscalResponse> EmitirAsync(NFeEmitirRequest request, CancellationToken ct = default)
    {
        try
        {
            request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
            ImpostoTributacaoCatalog.ValidarItensOuLancar(request.ConfiguracaoEmitente.Crt, request.Itens);

            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente);
            var nfe = ConstruirNFe(request, config);

            using var servicos = new ServicosNFe(config);
            var idLote = (int)(Math.Abs(DateTime.UtcNow.Ticks) % int.MaxValue);
            var retorno = servicos.NFeAutorizacao(idLote, IndicadorSincronizacao.Sincrono,
                new List<NFe.Classes.NFe> { nfe });

            var infProt = retorno.Retorno?.protNFe?.infProt;
            if (infProt is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno da SEFAZ não pôde ser processado.");

            var cStat = infProt.cStat.ToString();
            var xMotivo = infProt.xMotivo ?? string.Empty;
            var chave = infProt.chNFe ?? string.Empty;
            var protocolo = infProt.nProt ?? string.Empty;
            var autorizado = infProt.cStat == 100;

            if (!autorizado)
            {
                _logger.LogWarning("NF-e rejeitada: cStat={CStat} xMotivo={Motivo}", cStat, xMotivo);
                return FiscalResponse.Falha("RejeicaoSefaz", $"Rejeição SEFAZ: {xMotivo}", $"cStat: {cStat}");
            }

            var xmlAutorizado = retorno.RetornoStr ?? string.Empty;

            await RegistrarLogAsync(request.ConfiguracaoEmitente.Cnpj, "55", request.Serie,
                request.NumeroNota, chave, protocolo, "Autorizado", cStat, xMotivo,
                request.ConfiguracaoEmitente.Ambiente, ct);

            await SincronizarNumeracaoAsync(request.ConfiguracaoEmitente.Cnpj, "55",
                request.Serie, request.NumeroNota, ct);

            string? pdfBase64 = null;
            if (!string.IsNullOrWhiteSpace(xmlAutorizado))
            {
                try { pdfBase64 = _danfeService.GerarNFePdf(xmlAutorizado); }
                catch (Exception ex) { _logger.LogWarning(ex, "DANFE não gerado, prosseguindo sem PDF."); }
            }

            _logger.LogInformation("NF-e autorizada: Chave={Chave} Protocolo={Protocolo}", chave, protocolo);
            return FiscalResponse.Ok(chave, protocolo, cStat, xMotivo, xmlAutorizado, pdfBase64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao emitir NF-e para CNPJ={CNPJ}",
                request.ConfiguracaoEmitente?.Cnpj ?? request.EmitenteCnpj);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> CancelarAsync(NFeCancelarRequest request, CancellationToken ct = default)
    {
        try
        {
            request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
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
                return FiscalResponse.Falha("RejeicaoSefaz", $"Cancelamento rejeitado: {xMotivo}", $"cStat: {cStat}");

            await AtualizarStatusLogAsync(request.ChaveAcesso, "Cancelado", cStat, xMotivo, ct);
            _logger.LogInformation("NF-e cancelada: Chave={Chave} Protocolo={Protocolo}", request.ChaveAcesso, protocolo);
            return FiscalResponse.Ok(request.ChaveAcesso, protocolo, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar NF-e: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> CartaCorrecaoAsync(NFeCartaCorrecaoRequest request, CancellationToken ct = default)
    {
        request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
        return CartaCorrecaoCore(request);
    }

    private FiscalResponse CartaCorrecaoCore(NFeCartaCorrecaoRequest request)
    {
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente!);

            using var servicos = new ServicosNFe(config);
            var idLote = (int)(Math.Abs(DateTime.UtcNow.Ticks) % int.MaxValue);
            var retorno = servicos.RecepcaoEventoCartaCorrecao(
                idLote, request.SequenciaEvento,
                request.ChaveAcesso,
                request.Correcao,
                request.ConfiguracaoEmitente.Cnpj);

            var retEvento = retorno.Retorno?.retEvento?.FirstOrDefault()?.infEvento;
            if (retEvento is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno da CC-e não pôde ser processado.");

            var cStat = retEvento.cStat.ToString();
            var xMotivo = retEvento.xMotivo ?? string.Empty;
            var protocolo = retEvento.nProt ?? string.Empty;
            var sucesso = retEvento.cStat == 135;

            if (!sucesso)
                return FiscalResponse.Falha("RejeicaoSefaz", $"CC-e rejeitada: {xMotivo}", $"cStat: {cStat}");

            _logger.LogInformation("CC-e registrada: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Ok(request.ChaveAcesso, protocolo, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar CC-e: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> ConsultarAsync(NFeConsultarRequest request, CancellationToken ct = default)
    {
        request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
        return ConsultarCore(request);
    }

    private FiscalResponse ConsultarCore(NFeConsultarRequest request)
    {
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente!);

            using var servicos = new ServicosNFe(config);
            var retorno = servicos.NfeConsultaProtocolo(request.ChaveAcesso);

            var ret = retorno.Retorno;
            if (ret is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno da consulta não pôde ser processado.");

            var cStat = ret.cStat.ToString();
            var xMotivo = ret.xMotivo ?? string.Empty;
            var protocolo = ret.protNFe?.infProt?.nProt ?? string.Empty;

            _logger.LogInformation("Consulta NF-e: Chave={Chave} cStat={CStat}", request.ChaveAcesso, cStat);
            return new FiscalResponse
            {
                Sucesso = true,
                ChaveAcesso = request.ChaveAcesso,
                Protocolo = protocolo,
                CodigoStatus = cStat,
                Mensagem = xMotivo
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar NF-e: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> InutilizarAsync(NFeInutilizarRequest request, CancellationToken ct = default)
    {
        request.ConfiguracaoEmitente = await _emitenteService.ResolverConfiguracaoAsync(request, ct);
        return InutilizarCore(request);
    }

    private FiscalResponse InutilizarCore(NFeInutilizarRequest request)
    {
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente!);

            using var servicos = new ServicosNFe(config);
            var retorno = servicos.NfeInutilizacao(
                request.ConfiguracaoEmitente.Cnpj,
                DateTime.Now.Year % 100,
                ModeloDocumento.NFe,
                int.Parse(request.Serie),
                request.NumeroInicial,
                request.NumeroFinal,
                request.Justificativa);

            var ret = retorno.Retorno?.infInut;
            if (ret is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno da inutilização não pôde ser processado.");

            var cStat = ret.cStat.ToString();
            var xMotivo = ret.xMotivo ?? string.Empty;
            var protocolo = ret.nProt ?? string.Empty;
            var sucesso = ret.cStat == 102;

            if (!sucesso)
                return FiscalResponse.Falha("RejeicaoSefaz", $"Inutilização rejeitada: {xMotivo}", $"cStat: {cStat}");

            _logger.LogInformation("Inutilização autorizada: Serie={Serie} De={De} Ate={Ate}",
                request.Serie, request.NumeroInicial, request.NumeroFinal);
            return FiscalResponse.Ok(string.Empty, protocolo, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inutilizar faixa NF-e");
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public StatusServicoResponse ConsultarStatusSefaz(ConfiguracaoEmitenteRequest emitente)
    {
        try
        {
            var config = ConstruirConfiguracao(emitente);

            using var servicos = new ServicosNFe(config);
            var retorno = servicos.NfeStatusServico();

            var ret = retorno.Retorno;
            if (ret is null)
                return new StatusServicoResponse { Sucesso = false, Mensagem = "Sem retorno da SEFAZ." };

            return new StatusServicoResponse
            {
                Sucesso = ret.cStat == 107,
                CodigoStatus = ret.cStat.ToString(),
                Mensagem = ret.xMotivo,
                Uf = emitente.Uf,
                Modelo = "NFe",
                Ambiente = emitente.Ambiente,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status SEFAZ");
            return new StatusServicoResponse
            {
                Sucesso = false,
                Mensagem = ex.Message,
                Erro = new Models.Responses.ErroResponse
                {
                    Tipo = ClassificarExcecao(ex),
                    Mensagem = ex.Message,
                    Timestamp = DateTime.UtcNow
                }
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers internos
    // ─────────────────────────────────────────────────────────────────────────

    private ConfiguracaoServico ConstruirConfiguracao(ConfiguracaoEmitenteRequest emitente)
    {
        var config = new ConfiguracaoServico
        {
            cUF = UfHelper.MapearUf(emitente.Uf),
            tpAmb = emitente.Ambiente == "Producao" ? TipoAmbiente.Producao : TipoAmbiente.Homologacao,
            tpEmis = TipoEmissao.teNormal,
            ModeloDocumento = ModeloDocumento.NFe,
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

    private static NFe.Classes.NFe ConstruirNFe(NFeEmitirRequest req, ConfiguracaoServico config)
    {
        var emitente = req.ConfiguracaoEmitente;
        var uf = UfHelper.MapearUf(emitente.Uf);
        var dhEmissao = DateTimeOffset.Now;

        var itens = req.Itens.Select((item, idx) => ConstruirItem(item, idx + 1, req.ConfiguracaoEmitente.Crt)).ToList();
        var totalNota = CalcularTotais(req.Itens);

        return new NFe.Classes.NFe
        {
            infNFe = new infNFe
            {
                versao = "4.00",
                ide = new ide
                {
                    cUF = uf,
                    natOp = req.NaturezaOperacao,
                    mod = ModeloDocumento.NFe,
                    serie = int.Parse(req.Serie),
                    nNF = req.NumeroNota,
                    dhEmi = dhEmissao,
                    dhSaiEnt = dhEmissao,
                    tpNF = (TipoNFe)req.TipoOperacao,
                    idDest = (DestinoOperacao)req.IndicadorDestinatario,
                    cMunFG = long.TryParse(emitente.Endereco?.CodigoMunicipio, out var cMunFG) ? cMunFG : 0,
                    tpImp = TipoImpressao.tiRetrato,
                    tpEmis = TipoEmissao.teNormal,
                    tpAmb = config.tpAmb,
                    finNFe = (FinalidadeNFe)req.Finalidade,
                    indFinal = ConsumidorFinal.cfNao,
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
                        cPais = int.TryParse(emitente.Endereco.CodigoPais, out var cPaisEmit) ? cPaisEmit : 1058,
                        xPais = emitente.Endereco.Pais ?? "Brasil",
                        fone = long.TryParse(emitente.Endereco.Telefone, out var foneEmit) ? foneEmit : (long?)null
                    }
                },
                dest = ConstruirDestinatario(req.Destinatario),
                det = itens,
                total = new total
                {
                    ICMSTot = new ICMSTot
                    {
                        vBC = totalNota.BaseIcms,
                        vICMS = totalNota.Icms,
                        vICMSDeson = 0,
                        vFCP = 0,
                        vBCST = 0,
                        vST = 0,
                        vFCPST = 0,
                        vFCPSTRet = 0,
                        vProd = totalNota.Produtos,
                        vFrete = totalNota.Frete,
                        vSeg = totalNota.Seguro,
                        vDesc = totalNota.Desconto,
                        vII = 0,
                        vIPI = totalNota.Ipi,
                        vIPIDevol = 0,
                        vPIS = totalNota.Pis,
                        vCOFINS = totalNota.Cofins,
                        vOutro = totalNota.Outras,
                        vNF = totalNota.TotalNota,
                        vTotTrib = 0,
                        vFCPUFDest = totalNota.FcpUfDest > 0 ? totalNota.FcpUfDest : null,
                        vICMSUFDest = totalNota.IcmsUfDest > 0 ? totalNota.IcmsUfDest : null,
                        vICMSUFRemet = totalNota.IcmsUfRemet > 0 ? totalNota.IcmsUfRemet : null
                    }
                },
                transp = new transp
                {
                    modFrete = (ModalidadeFrete)req.ModalidadeFrete
                },
                pag = new List<pag>
                {
                    new pag
                    {
                        detPag = req.Pagamentos.Select(p => new detPag
                        {
                            tPag = (FormaPagamento)int.Parse(p.FormaPagamento),
                            vPag = p.ValorPagamento
                        }).ToList()
                    }
                },
                infAdic = string.IsNullOrWhiteSpace(req.InformacoesAdicionais) ? null : new infAdic
                {
                    infCpl = req.InformacoesAdicionais
                }
            }
        };
    }

    private static dest ConstruirDestinatario(DestinatarioRequest dto)
    {
        var d = new dest(VersaoServico.Versao400)
        {
            xNome = dto.RazaoSocial ?? "NÃO IDENTIFICADO",
            indIEDest = (indIEDest)dto.IndicadorIe,
            IE = dto.Ie,
            email = dto.Email
        };

        if (!string.IsNullOrWhiteSpace(dto.Cnpj))
            d.CNPJ = dto.Cnpj;
        else if (!string.IsNullOrWhiteSpace(dto.Cpf))
            d.CPF = dto.Cpf;

        if (dto.Endereco is not null)
        {
            d.enderDest = new enderDest
            {
                xLgr = dto.Endereco.Logradouro ?? string.Empty,
                nro = dto.Endereco.Numero ?? string.Empty,
                xCpl = dto.Endereco.Complemento,
                xBairro = dto.Endereco.Bairro ?? string.Empty,
                cMun = long.TryParse(dto.Endereco.CodigoMunicipio, out var cMunDest) ? cMunDest : 0,
                xMun = dto.Endereco.Municipio ?? string.Empty,
                UF = dto.Endereco.Uf ?? "RS",
                CEP = dto.Endereco.Cep ?? string.Empty,
                cPais = int.TryParse(dto.Endereco.CodigoPais, out var cPaisDest) ? cPaisDest : 1058,
                xPais = dto.Endereco.Pais ?? "Brasil",
                fone = long.TryParse(dto.Endereco.Telefone, out var foneDest) ? foneDest : (long?)null
            };
        }

        return d;
    }

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
                CEST = item.Cest,
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
                vFrete = item.ValorFrete,
                vSeg = item.ValorSeguro,
                vOutro = item.ValorOutrasDespesas,
                indTot = item.IndicadorTotal ? IndicadorTotal.ValorDoItemCompoeTotalNF : IndicadorTotal.ValorDoItemNaoCompoeTotalNF
            },
            imposto = ImpostoItemFactory.Criar(item, crt)
        };
    }

    private static (decimal BaseIcms, decimal Icms, decimal Produtos, decimal Frete, decimal Seguro,
                    decimal Desconto, decimal Ipi, decimal Pis, decimal Cofins, decimal Outras, decimal TotalNota,
                    decimal FcpUfDest, decimal IcmsUfDest, decimal IcmsUfRemet)
        CalcularTotais(List<ItemNFeRequest> itens)
    {
        return (
            BaseIcms: itens.Sum(i => i.BaseCalculoIcms ?? 0),
            Icms: itens.Sum(i => i.ValorIcms ?? 0),
            Produtos: itens.Sum(i => i.ValorTotalBruto),
            Frete: itens.Sum(i => i.ValorFrete ?? 0),
            Seguro: itens.Sum(i => i.ValorSeguro ?? 0),
            Desconto: itens.Sum(i => i.ValorDesconto ?? 0),
            Ipi: itens.Sum(i => i.ValorIpi ?? 0),
            Pis: itens.Sum(i => i.ValorPis ?? 0),
            Cofins: itens.Sum(i => i.ValorCofins ?? 0),
            Outras: itens.Sum(i => i.ValorOutrasDespesas ?? 0),
            TotalNota: itens.Sum(i => i.ValorTotalBruto)
                - itens.Sum(i => i.ValorDesconto ?? 0)
                + itens.Sum(i => i.ValorFrete ?? 0)
                + itens.Sum(i => i.ValorSeguro ?? 0)
                + itens.Sum(i => i.ValorOutrasDespesas ?? 0),
            FcpUfDest: itens.Sum(i => i.ValorFcpUfDest ?? 0),
            IcmsUfDest: itens.Sum(i => i.ValorIcmsUfDest ?? 0),
            IcmsUfRemet: itens.Sum(i => i.ValorIcmsUfRemet ?? 0)
        );
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
            _logger.LogWarning(ex, "Falha ao salvar log de emissão — operação fiscal não comprometida.");
        }
    }

    private async Task SincronizarNumeracaoAsync(string cnpj, string modelo, string serie, int numero, CancellationToken ct)
    {
        try
        {
            await _numeracaoService.ConfirmarNumeroAsync(cnpj, modelo, serie, numero, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao sincronizar numeração após emissão: CNPJ={CNPJ} Modelo={Modelo} Serie={Serie} Numero={Numero}",
                cnpj, modelo, serie, numero);
        }
    }

    private async Task AtualizarStatusLogAsync(string chave, string novoStatus, string cStat, string mensagem, CancellationToken ct)
    {
        try
        {
            var log = await _db.EmissaoLogs.FirstOrDefaultAsync(e => e.ChaveAcesso == chave, ct);
            if (log is not null)
            {
                log.Status = novoStatus;
                log.CodigoStatus = cStat;
                log.MensagemStatus = mensagem;
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao atualizar log de emissão.");
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
        if (msg.Contains("certificado") || msg.Contains("pfx") || msg.Contains("senha"))
            return "CertificadoInvalido";
        if (msg.Contains("timeout") || msg.Contains("unavailable") || msg.Contains("connection"))
            return "ServicoIndisponivel";
        if (msg.Contains("schema") || msg.Contains("xml"))
            return "ValidacaoSchema";
        return "ErroInterno";
    }
}
