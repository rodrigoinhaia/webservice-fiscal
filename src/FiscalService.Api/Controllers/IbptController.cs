using FiscalService.Api.Config;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services;
using FiscalService.Api.Services.Ibpt;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/ibpt")]
[Produces("application/json")]
public class IbptController : ControllerBase
{
    private readonly FiscalConfig _config;
    private readonly IbptTabelaArquivoStore _tabela;
    private readonly IIbptAliquotaLookup _lookup;
    private readonly EmitenteService _emitenteService;

    public IbptController(
        FiscalConfig config,
        IbptTabelaArquivoStore tabela,
        IIbptAliquotaLookup lookup,
        EmitenteService emitenteService)
    {
        _config = config;
        _tabela = tabela;
        _lookup = lookup;
        _emitenteService = emitenteService;
    }

    /// <summary>Status da integração Lei 12.741/2012 (IBPT / De Olho no Imposto).</summary>
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new IbptStatusResponse
        {
            Habilitado = _config.Ibpt.Habilitado,
            PossuiTokenGlobal = !string.IsNullOrWhiteSpace(_config.Ibpt.Token),
            TabelaCarregada = _tabela.Carregada,
            TabelaRegistros = _tabela.QuantidadeRegistros,
            TabelaCaminho = _tabela.Caminho,
            UrlProdutos = _config.Ibpt.UrlProdutos,
            IncluirInfCpl = _config.Ibpt.IncluirInfCpl,
            Obrigatorio = _config.Ibpt.Obrigatorio,
            Observacao = "Token é por CNPJ. Cadastre em PUT /api/emitentes/{cnpj} (ibptToken) " +
                         "ou use Fiscal__Ibpt__Token. A API oficial pode ficar indisponível — use a tabela local."
        });
    }

    /// <summary>Consulta carga tributária aproximada de um NCM (API IBPT ou tabela local).</summary>
    [HttpGet("produtos")]
    public async Task<IActionResult> ConsultarProduto(
        [FromQuery] string ncm,
        [FromQuery] string uf,
        [FromQuery] decimal valor = 0,
        [FromQuery] string? cnpj = null,
        [FromQuery] int ex = 0,
        [FromQuery] string? descricao = null,
        [FromQuery] string? unidade = "UN",
        [FromQuery] string? gtin = "SEM GTIN",
        [FromQuery] string? origemMercadoria = "0",
        [FromQuery] string? token = null,
        CancellationToken ct = default)
    {
        var ncmNorm = IbptTributoCalculator.NormalizarNcm(ncm);
        if (ncmNorm.Length == 0)
            return BadRequest(new { sucesso = false, erro = new { tipo = "Validacao", mensagem = "Informe ncm." } });
        if (string.IsNullOrWhiteSpace(uf) || uf.Trim().Length != 2)
            return BadRequest(new { sucesso = false, erro = new { tipo = "Validacao", mensagem = "Informe uf (2 letras)." } });

        var credencial = await ResolverCredencialAsync(cnpj, token, ct);
        var aliquota = await _lookup.ObterAsync(
            credencial,
            new IbptConsultaChave(ncmNorm, uf.Trim().ToUpperInvariant(), ex),
            descricao ?? "PRODUTO",
            unidade ?? "UN",
            valor,
            gtin ?? "SEM GTIN",
            ct);

        if (aliquota is null)
        {
            return Ok(new IbptConsultaResponse
            {
                Encontrado = false,
                Ncm = ncmNorm,
                Uf = uf.Trim().ToUpperInvariant(),
                Ex = ex,
                Aviso = "NCM não encontrado na tabela local nem na API IBPT. " +
                        "Confira o token (CNPJ), a UF e, se a API estiver fora, importe a tabela do portal."
            });
        }

        var item = new ItemNFeRequest
        {
            Ncm = ncmNorm,
            ValorTotalBruto = valor,
            OrigemMercadoria = origemMercadoria,
            UnidadeComercial = unidade ?? "UN",
            DescricaoProduto = descricao ?? "PRODUTO",
            CodigoEan = gtin
        };
        var calc = IbptTributoCalculator.CalcularItem(item, aliquota);

        return Ok(new IbptConsultaResponse
        {
            Encontrado = true,
            Ncm = aliquota.Codigo,
            Uf = aliquota.Uf,
            Ex = aliquota.Ex,
            Descricao = aliquota.Descricao,
            AliquotaNacional = aliquota.Nacional,
            AliquotaImportado = aliquota.Importado,
            AliquotaEstadual = aliquota.Estadual,
            AliquotaMunicipal = aliquota.Municipal,
            ValorFederal = calc.Federal,
            ValorEstadual = calc.Estadual,
            ValorMunicipal = calc.Municipal,
            ValorTotal = calc.Total,
            OrigemImportada = calc.Importado,
            Fonte = aliquota.Fonte,
            Versao = aliquota.Versao,
            Chave = aliquota.Chave,
            OrigemDados = aliquota.Origem,
            InfCpl = IbptTributoCalculator.MontarInfCpl(calc.Federal, calc.Estadual, calc.Municipal, aliquota.Fonte, aliquota.Versao)
        });
    }

    /// <summary>Relê a tabela CSV/TXT configurada em <c>Fiscal:Ibpt:ArquivoTabela</c>.</summary>
    [HttpPost("tabela/recarregar")]
    public IActionResult RecarregarTabela()
    {
        _tabela.Recarregar();
        return Ok(new
        {
            sucesso = true,
            registros = _tabela.QuantidadeRegistros,
            caminho = _tabela.Caminho
        });
    }

    private async Task<IbptCredencial> ResolverCredencialAsync(string? cnpj, string? tokenOverride, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(tokenOverride))
            return new IbptCredencial(cnpj ?? "", tokenOverride.Trim());

        if (!string.IsNullOrWhiteSpace(cnpj))
        {
            try
            {
                var source = new NFeEmitirRequest { EmitenteCnpj = cnpj };
                var cfg = await _emitenteService.ResolverConfiguracaoAsync(source, ct);
                if (!string.IsNullOrWhiteSpace(cfg.IbptToken))
                    return new IbptCredencial(cfg.Cnpj, cfg.IbptToken);
                return new IbptCredencial(cfg.Cnpj, _config.Ibpt.Token ?? "");
            }
            catch (KeyNotFoundException)
            {
                // emitente não cadastrado — usa token global
            }
        }

        return new IbptCredencial(cnpj ?? "", _config.Ibpt.Token ?? "");
    }
}
