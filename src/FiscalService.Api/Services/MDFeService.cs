using DFe.Classes.Entidades;
using DFe.Classes.Flags;
using DFe.Utils;
using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.Helpers;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using MDFe.Classes.Flags;
using MDFe.Classes.Informacoes;
using MDFe.Servicos.EventosMDFe;
using MDFe.Servicos.RecepcaoMDFe;
using MDFe.Servicos.StatusServicoMDFe;
using MDFe.Utils.Configuracoes;

namespace FiscalService.Api.Services;

/// <summary>
/// Orquestra emissão, encerramento e cancelamento de MDF-e 3.0.
/// ATENÇÃO: Não usar como Singleton — DFe.NET não é thread-safe.
/// </summary>
public class MDFeService
{
    private readonly FiscalConfig _globalConfig;
    private readonly AppDbContext _db;
    private readonly NumeracaoService _numeracaoService;
    private readonly ILogger<MDFeService> _logger;

    public MDFeService(FiscalConfig globalConfig, AppDbContext db, NumeracaoService numeracaoService, ILogger<MDFeService> logger)
    {
        _globalConfig = globalConfig;
        _db = db;
        _numeracaoService = numeracaoService;
        _logger = logger;
    }

    public async Task<FiscalResponse> EmitirAsync(MDFeEmitirRequest request, CancellationToken ct = default)
    {
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente);
            var mdfe = ConstruirMDFe(request, config);

            var servico = new ServicoMDFeRecepcao();
            var retorno = servico.MDFeRecepcaoSinc(mdfe, config);

            if (retorno is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno da SEFAZ não pôde ser processado.");

            var cStat = retorno.CStat.ToString();
            var xMotivo = retorno.XMotivo ?? string.Empty;
            var chave = retorno.ProtMdFe?.InfProt?.ChMDFe ?? string.Empty;
            var protocolo = retorno.ProtMdFe?.InfProt?.NProt ?? string.Empty;
            var autorizado = retorno.CStat == 100;

            if (!autorizado)
                return FiscalResponse.Falha("RejeicaoSefaz", $"MDF-e rejeitado: {xMotivo}", $"cStat: {cStat}");

            await RegistrarLogAsync(request.ConfiguracaoEmitente.Cnpj, "58", request.Serie,
                request.NumeroNota, chave, protocolo, "Autorizado", cStat, xMotivo,
                request.ConfiguracaoEmitente.Ambiente, ct);

            await SincronizarNumeracaoAsync(request.ConfiguracaoEmitente.Cnpj, "58",
                request.Serie, request.NumeroNota, ct);

            _logger.LogInformation("MDF-e autorizado: Chave={Chave}", chave);
            return FiscalResponse.Ok(chave, protocolo, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao emitir MDF-e");
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public Task<FiscalResponse> EncerrarAsync(MDFeEncerrarRequest request, CancellationToken ct = default) =>
        Task.FromResult(EncerrarCore(request));

    private FiscalResponse EncerrarCore(MDFeEncerrarRequest request)
    {
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente);

            var mdfe = new MDFe.Classes.Informacoes.MDFe();
            mdfe.InfMDFe.Id = $"MDFe{request.ChaveAcesso}";

            var ufEncerramento = UfHelper.MapearUf(request.UfEncerramento);
            var codigoMunicipio = long.TryParse(request.CodigoMunicipioEncerramento, out var cMun) ? cMun : 0;

            var servico = new EventoEncerramento();
            var retorno = servico.MDFeEventoEncerramento(mdfe, ufEncerramento, codigoMunicipio, 1, request.Protocolo, config);

            if (retorno is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno do encerramento não pôde ser processado.");

            var cStat = retorno.InfEvento?.CStat.ToString() ?? "0";
            var xMotivo = retorno.InfEvento?.XMotivo ?? string.Empty;
            var protocolo = retorno.InfEvento?.NProt ?? string.Empty;
            var sucesso = retorno.InfEvento?.CStat == 135;

            if (!sucesso)
                return FiscalResponse.Falha("RejeicaoSefaz", $"Encerramento MDF-e rejeitado: {xMotivo}", $"cStat: {cStat}");

            _logger.LogInformation("MDF-e encerrado: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Ok(request.ChaveAcesso, protocolo, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao encerrar MDF-e");
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    public Task<FiscalResponse> CancelarAsync(MDFeCancelarRequest request, CancellationToken ct = default) =>
        Task.FromResult(CancelarMdfeCore(request));

    /// <summary>Consulta status do serviço SEFAZ MDF-e (modelo 58).</summary>
    public StatusServicoResponse ConsultarStatusSefaz(ConfiguracaoEmitenteRequest emitente)
    {
        try
        {
            var config = ConstruirConfiguracao(emitente);
            var ret = new ServicoMDFeStatusServico().MDFeStatusServico(config);

            if (ret is null)
                return new StatusServicoResponse { Sucesso = false, Mensagem = "Sem retorno da SEFAZ." };

            return new StatusServicoResponse
            {
                Sucesso = ret.CStat == 107,
                CodigoStatus = ret.CStat.ToString(),
                Mensagem = ret.XMotivo,
                Uf = emitente.Uf,
                Modelo = "MDFe",
                Ambiente = emitente.Ambiente,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status SEFAZ MDF-e");
            return new StatusServicoResponse
            {
                Sucesso = false,
                Mensagem = ex.Message,
                Erro = new ErroResponse { Tipo = ClassificarExcecao(ex), Mensagem = ex.Message, Timestamp = DateTime.UtcNow }
            };
        }
    }

    private FiscalResponse CancelarMdfeCore(MDFeCancelarRequest request)
    {
        try
        {
            var config = ConstruirConfiguracao(request.ConfiguracaoEmitente);

            var mdfe = new MDFe.Classes.Informacoes.MDFe();
            mdfe.InfMDFe.Id = $"MDFe{request.ChaveAcesso}";

            var servico = new EventoCancelar();
            var retorno = servico.MDFeEventoCancelar(mdfe, 1, request.Protocolo, request.Justificativa, config);

            if (retorno is null)
                return FiscalResponse.Falha("ErroInterno", "Retorno do cancelamento não pôde ser processado.");

            var cStat = retorno.InfEvento?.CStat.ToString() ?? "0";
            var xMotivo = retorno.InfEvento?.XMotivo ?? string.Empty;
            var protocolo = retorno.InfEvento?.NProt ?? string.Empty;
            var sucesso = retorno.InfEvento?.CStat == 135;

            if (!sucesso)
                return FiscalResponse.Falha("RejeicaoSefaz", $"Cancelamento MDF-e rejeitado: {xMotivo}", $"cStat: {cStat}");

            _logger.LogInformation("MDF-e cancelado: Chave={Chave}", request.ChaveAcesso);
            return FiscalResponse.Ok(request.ChaveAcesso, protocolo, cStat, xMotivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar MDF-e");
            return FiscalResponse.Falha(ClassificarExcecao(ex), ex.Message, ex.ToString());
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private MDFeConfiguracao ConstruirConfiguracao(ConfiguracaoEmitenteRequest emitente)
    {
        var config = new MDFeConfiguracao();

        config.ConfiguracaoCertificado.TipoCertificado = TipoCertificado.A1Arquivo;
        config.ConfiguracaoCertificado.Arquivo = _globalConfig.ResolveCertificadoPath(emitente.CertificadoPath);
        config.ConfiguracaoCertificado.Senha = emitente.CertificadoSenha;

        config.IsSalvarXml = _globalConfig.SalvarXmls;
        config.CaminhoSalvarXml = _globalConfig.DiretorioXmls;
        config.CaminhoSchemas = _globalConfig.DiretorioSchemas;

        config.VersaoWebService.UfEmitente = UfHelper.MapearUf(emitente.Uf);
        config.VersaoWebService.TipoAmbiente =
            emitente.Ambiente == "Producao" ? TipoAmbiente.Producao : TipoAmbiente.Homologacao;
        config.VersaoWebService.TimeOut = _globalConfig.TimeoutWs;

        return config;
    }

    private static MDFe.Classes.Informacoes.MDFe ConstruirMDFe(MDFeEmitirRequest req, MDFeConfiguracao config)
    {
        var emitente = req.ConfiguracaoEmitente;
        var uf = UfHelper.MapearUf(emitente.Uf);

        var mdfe = new MDFe.Classes.Informacoes.MDFe();
        var inf = mdfe.InfMDFe;

        inf.Ide.CUF = uf;
        inf.Ide.TpAmb = emitente.Ambiente == "Producao" ? TipoAmbiente.Producao : TipoAmbiente.Homologacao;
        inf.Ide.TpEmit = MDFeTipoEmitente.TransportadorCargaPropria;
        inf.Ide.Mod = ModeloDocumento.MDFe;
        inf.Ide.Serie = short.Parse(req.Serie);
        inf.Ide.NMDF = req.NumeroNota;
        inf.Ide.DhEmi = DateTime.Now;
        inf.Ide.TpEmis = MDFeTipoEmissao.Normal;
        inf.Ide.Modal = MDFeModal.Rodoviario;
        inf.Ide.UFIni = UfHelper.MapearUf(req.UfInicio);
        inf.Ide.UFFim = UfHelper.MapearUf(req.UfFim);
        inf.Ide.DhIniViagem = req.DataHoraInicio;

        inf.Ide.InfMunCarrega = req.MunicipiosCarregamento.Select(m => new MDFeInfMunCarrega
        {
            CMunCarrega = m.CodigoMunicipio ?? string.Empty,
            XMunCarrega = m.NomeMunicipio
        }).ToList();

        inf.Ide.InfPercurso = req.Percurso.Select(p => new MDFeInfPercurso
        {
            UFPer = UfHelper.MapearUf(p.Uf)
        }).ToList();

        inf.Emit.CNPJ = emitente.Cnpj;
        inf.Emit.IE = emitente.Ie ?? string.Empty;
        inf.Emit.XNome = emitente.RazaoSocial;
        inf.Emit.XFant = emitente.NomeFantasia;

        if (emitente.Endereco is not null)
        {
            inf.Emit.EnderEmit = new MDFeEnderEmit
            {
                XLgr = emitente.Endereco.Logradouro ?? string.Empty,
                Nro = emitente.Endereco.Numero ?? string.Empty,
                XCpl = emitente.Endereco.Complemento,
                XBairro = emitente.Endereco.Bairro ?? string.Empty,
                CMun = long.TryParse(emitente.Endereco.CodigoMunicipio, out var cMunEmit) ? cMunEmit : 0,
                XMun = emitente.Endereco.Municipio ?? string.Empty,
                CEP = long.TryParse((emitente.Endereco.Cep ?? "").Replace("-", ""), out var cepVal) ? cepVal : 0,
                UF = uf,
                Fone = emitente.Endereco.Telefone
            };
        }

        inf.InfDoc.InfMunDescarga = req.Documentos
            .GroupBy(d => d.CodigoMunicipioDescarga)
            .Select(g => new MDFeInfMunDescarga
            {
                CMunDescarga = g.Key,
                XMunDescarga = g.First().NomeMunicipioDescarga,
                InfCTe = g.Where(d => d.TipoDocumento == "CTe")
                    .Select(d => new MDFeInfCTe { ChCTe = d.ChaveAcesso }).ToList(),
                InfNFe = g.Where(d => d.TipoDocumento == "NFe")
                    .Select(d => new MDFeInfNFe { ChNFe = d.ChaveAcesso }).ToList()
            }).ToList();

        inf.InfModal.VersaoModal = MDFeVersaoModal.Versao300;
        inf.InfModal.Modal = new MDFeRodo
        {
            RNTRC = string.Empty,
            VeicTracao = new MDFeVeicTracao
            {
                CInt = string.Empty,
                Placa = string.Empty,
                RENAVAM = string.Empty,
                TpRod = MDFeTpRod.Truck,
                TpCar = MDFeTpCar.Granelera,
                UF = uf
            }
        };

        return mdfe;
    }

    private async Task SincronizarNumeracaoAsync(string cnpj, string modelo, string serie, int numero, CancellationToken ct)
    {
        try
        {
            await _numeracaoService.ConfirmarNumeroAsync(cnpj, modelo, serie, numero, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao sincronizar numeração MDF-e: CNPJ={CNPJ} Modelo={Modelo} Serie={Serie} Numero={Numero}",
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
                Cnpj = cnpj, Modelo = modelo, Serie = serie, Numero = numero,
                ChaveAcesso = chave, Protocolo = protocolo, Status = status,
                CodigoStatus = cStat, MensagemStatus = mensagem, Ambiente = ambiente,
                DataEmissao = DateTime.UtcNow, DataProcessamento = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Falha ao salvar log MDF-e."); }
    }

    private static string ClassificarExcecao(Exception ex)
    {
        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("certificado") || msg.Contains("pfx") || msg.Contains("senha")) return "CertificadoInvalido";
        if (msg.Contains("timeout") || msg.Contains("unavailable") || msg.Contains("connection")) return "ServicoIndisponivel";
        return "ErroInterno";
    }
}
