using CTe.Classes;
using CTe.Classes.Informacoes;
using CTe.Classes.Informacoes.Complemento;
using CTe.Classes.Informacoes.Destinatario;
using CTe.Classes.Informacoes.Emitente;
using CTe.Classes.Informacoes.Identificacao;
using CTe.Classes.Informacoes.Impostos;
using CTe.Classes.Informacoes.Impostos.ICMS;
using CTe.Classes.Informacoes.Impostos.Tributacao;
using CTe.Classes.Informacoes.Remetente;
using CTe.Classes.Informacoes.Tipos;
using CTe.Classes.Informacoes.Valores;
using CTe.Classes.Servicos.Recepcao.Retorno;
using CTe.Servicos.EnviarCte;
using CTe.Servicos.Eventos;
using CTe.Utils.CTe;
using DFe.Classes.Flags;
using DFe.Utils;
using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.Helpers;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;

namespace FiscalService.Api.Services;

/// <summary>
/// Orquestra emissão e cancelamento de CT-e 4.0.
/// ATENÇÃO: Não usar como Singleton — DFe.NET não é thread-safe.
/// </summary>
public class CTeService
{
    private readonly FiscalConfig _globalConfig;
    private readonly AppDbContext _db;
    private readonly ILogger<CTeService> _logger;

    public CTeService(FiscalConfig globalConfig, AppDbContext db, ILogger<CTeService> logger)
    {
        _globalConfig = globalConfig;
        _db = db;
        _logger = logger;
    }

    public async Task<FiscalResponse> EmitirAsync(CTeEmitirRequest request, CancellationToken ct = default)
    {
        try
        {
            ConfigurarSingleton(request.ConfiguracaoEmitente);
            var cte = ConstruirCTe(request);

            var lote = (int)(Math.Abs(DateTime.UtcNow.Ticks) % int.MaxValue);
            var retorno = new ServicoEnviarCte().Enviar(lote, cte);

            if (retorno?.CteProc?.protCTe?.infProt is null)
            {
                var cStatEnv = retorno?.RetEnviCte?.cStat.ToString() ?? "0";
                var xMotivoEnv = retorno?.RetEnviCte?.xMotivo ?? "Retorno não processado";
                return FiscalResponse.Falha("ErroInterno", xMotivoEnv, $"cStat: {cStatEnv}");
            }

            var infProt = retorno.CteProc.protCTe.infProt;
            var cStat = infProt.cStat.ToString();
            var xMotivo = infProt.xMotivo ?? string.Empty;
            var chave = infProt.chCTe ?? string.Empty;
            var protocolo = infProt.nProt ?? string.Empty;
            var autorizado = infProt.cStat == 100;

            if (!autorizado)
                return FiscalResponse.Falha("RejeicaoSefaz", $"CT-e rejeitado: {xMotivo}", $"cStat: {cStat}");

            await RegistrarLogAsync(request.ConfiguracaoEmitente.Cnpj, "57", request.Serie,
                request.NumeroNota, chave, protocolo, "Autorizado", cStat, xMotivo,
                request.ConfiguracaoEmitente.Ambiente, ct);

            _logger.LogInformation("CT-e autorizado: Chave={Chave}", chave);
            return FiscalResponse.Ok(chave, protocolo, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao emitir CT-e");
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public async Task<FiscalResponse> CancelarAsync(NFeCancelarRequest request, CancellationToken ct = default)
    {
        try
        {
            ConfigurarSingleton(request.ConfiguracaoEmitente);

            var cte = new CTe.Classes.CTe
            {
                infCte = new infCte
                {
                    ide = new ide(),
                    emit = new emit { CNPJ = request.ConfiguracaoEmitente.Cnpj }
                }
            };

            var cancelamento = new EventoCancelamento(cte, 1, request.Protocolo, request.Justificativa);
            var retEvento = cancelamento.Cancelar();

            if (retEvento is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno do cancelamento não pôde ser processado.");

            var cStat = retEvento.infEvento?.cStat.ToString() ?? "0";
            var xMotivo = retEvento.infEvento?.xMotivo ?? string.Empty;
            var sucesso = retEvento.infEvento?.cStat == 135;

            if (!sucesso)
                return FiscalResponse.Falha("RejeicaoSefaz", $"Cancelamento CT-e rejeitado: {xMotivo}", $"cStat: {cStat}");

            _logger.LogInformation("CT-e cancelado: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Ok(request.ChaveAcesso, string.Empty, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar CT-e");
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void ConfigurarSingleton(ConfiguracaoEmitenteRequest emitente)
    {
        var config = ConfiguracaoServico.Instancia;

        config.ConfiguracaoCertificado.TipoCertificado = TipoCertificado.A1Arquivo;
        config.ConfiguracaoCertificado.Arquivo = _globalConfig.ResolveCertificadoPath(emitente.CertificadoPath);
        config.ConfiguracaoCertificado.Senha = emitente.CertificadoSenha;

        config.tpAmb = emitente.Ambiente == "Producao" ? TipoAmbiente.Producao : TipoAmbiente.Homologacao;
        config.cUF = UfHelper.MapearUf(emitente.Uf);
        config.IsSalvarXml = _globalConfig.SalvarXmls;
        config.DiretorioSalvarXml = _globalConfig.DiretorioXmls;
        config.DiretorioSchemas = _globalConfig.DiretorioSchemas;
        config.TimeOut = _globalConfig.TimeoutWs;
    }

    private static CTe.Classes.CTe ConstruirCTe(CTeEmitirRequest req)
    {
        var emitente = req.ConfiguracaoEmitente;

        return new CTe.Classes.CTe
        {
            infCte = new infCte
            {
                ide = new ide
                {
                    cUF = UfHelper.MapearUf(emitente.Uf),
                    natOp = req.NaturezaOperacao,
                    mod = ModeloDocumento.CTe,
                    serie = (short)int.Parse(req.Serie),
                    nCT = req.NumeroNota,
                    dhEmi = DateTime.Now,
                    tpCTe = tpCTe.Normal,
                    CFOP = int.Parse(req.Cfop ?? "5353"),
                    modal = (modal)int.Parse(req.Modal),
                    tpServ = tpServ.normal,
                    tpAmb = ConfiguracaoServico.Instancia.tpAmb,
                    procEmi = procEmi.AplicativoContribuinte,
                    verProc = "1.0"
                },
                compl = req.InformacoesAdicionais != null ? new compl { xObs = req.InformacoesAdicionais } : null,
                emit = new emit
                {
                    CNPJ = emitente.Cnpj,
                    IE = emitente.Ie,
                    xNome = emitente.RazaoSocial,
                    enderEmit = emitente.Endereco is null ? null : new enderEmit
                    {
                        xLgr = emitente.Endereco.Logradouro ?? string.Empty,
                        nro = emitente.Endereco.Numero ?? string.Empty,
                        xBairro = emitente.Endereco.Bairro ?? string.Empty,
                        cMun = long.TryParse(emitente.Endereco.CodigoMunicipio, out var cMunEmit) ? cMunEmit : 0,
                        xMun = emitente.Endereco.Municipio ?? string.Empty,
                        CEP = long.TryParse((emitente.Endereco.Cep ?? "").Replace("-", ""), out var cepEmit) ? cepEmit : 0,
                        UF = UfHelper.MapearUf(emitente.Uf)
                    }
                },
                rem = new rem
                {
                    CNPJ = req.Remetente.Cnpj,
                    xNome = req.Remetente.RazaoSocial,
                    IE = string.Empty,
                    fone = string.Empty,
                    enderReme = req.Remetente.Endereco is null ? null : new enderReme
                    {
                        xLgr = req.Remetente.Endereco.Logradouro ?? string.Empty,
                        nro = req.Remetente.Endereco.Numero ?? string.Empty,
                        xBairro = req.Remetente.Endereco.Bairro ?? string.Empty,
                        cMun = long.TryParse(req.Remetente.Endereco.CodigoMunicipio, out var cMunRem) ? cMunRem : 0,
                        xMun = req.Remetente.Endereco.Municipio ?? string.Empty,
                        CEP = long.TryParse((req.Remetente.Endereco.Cep ?? "").Replace("-", ""), out var cepRem) ? cepRem : 0,
                        UF = UfHelper.MapearUf(req.Remetente.Endereco.Uf ?? emitente.Uf),
                        cPais = 1058,
                        xPais = "Brasil"
                    }
                },
                dest = new dest
                {
                    CNPJ = req.Destinatario.Cnpj,
                    xNome = req.Destinatario.RazaoSocial ?? string.Empty,
                    IE = req.Destinatario.Ie,
                    enderDest = req.Destinatario.Endereco is null ? null : new enderDest
                    {
                        xLgr = req.Destinatario.Endereco.Logradouro ?? string.Empty,
                        nro = req.Destinatario.Endereco.Numero ?? string.Empty,
                        xBairro = req.Destinatario.Endereco.Bairro ?? string.Empty,
                        cMun = long.TryParse(req.Destinatario.Endereco.CodigoMunicipio, out var cMunDest) ? cMunDest : 0,
                        xMun = req.Destinatario.Endereco.Municipio ?? string.Empty,
                        CEP = long.TryParse((req.Destinatario.Endereco.Cep ?? "").Replace("-", ""), out var cepDest) ? cepDest : 0,
                        UF = UfHelper.MapearUf(req.Destinatario.Endereco.Uf ?? emitente.Uf),
                        cPais = 1058,
                        xPais = "Brasil"
                    }
                },
                vPrest = new vPrest
                {
                    vTPrest = req.ValorTotalServico,
                    vRec = req.ValorTotalServico
                },
                imp = new imp
                {
                    ICMS = new ICMS
                    {
                        TipoICMS = new ICMS00
                        {
                            vBC = req.ValorTotalServico,
                            pICMS = 12,
                            vICMS = req.ValorTotalServico * 0.12m
                        }
                    },
                    vTotTrib = 0
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
                Cnpj = cnpj, Modelo = modelo, Serie = serie, Numero = numero,
                ChaveAcesso = chave, Protocolo = protocolo, Status = status,
                CodigoStatus = cStat, MensagemStatus = mensagem, Ambiente = ambiente,
                DataEmissao = DateTime.UtcNow, DataProcessamento = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Falha ao salvar log CT-e."); }
    }

    private static string ClassificarExcecao(Exception ex)
    {
        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("certificado") || msg.Contains("pfx") || msg.Contains("senha")) return "CertificadoInvalido";
        if (msg.Contains("timeout") || msg.Contains("unavailable") || msg.Contains("connection")) return "ServicoIndisponivel";
        return "ErroInterno";
    }
}
