using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/emissoes")]
[Produces("application/json")]
public class EmissoesController : ControllerBase
{
    private readonly EmissaoLogService _service;

    public EmissoesController(EmissaoLogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lista emissões fiscais (NF-e/NFC-e/CT-e/MDF-e) com filtros opcionais e paginação.
    /// Ordenação: <c>dataEmissao DESC</c>.
    /// </summary>
    /// <param name="cnpj">Filtra pelo CNPJ do emitente (14 dígitos sem máscara).</param>
    /// <param name="modelo">Modelo do documento: <c>55</c>, <c>65</c>, <c>57</c> ou <c>58</c>.</param>
    /// <param name="serie">Série do documento.</param>
    /// <param name="ambiente"><c>Homologacao</c> ou <c>Producao</c>.</param>
    /// <param name="status"><c>Autorizado</c>, <c>Cancelado</c>, <c>Rejeitado</c>, <c>Inutilizado</c>.</param>
    /// <param name="chave">Chave de acesso de 44 dígitos.</param>
    /// <param name="dataDe">Janela inicial (UTC, inclusiva).</param>
    /// <param name="dataAte">Janela final (UTC, inclusiva).</param>
    /// <param name="pagina">Página (1-based, default 1).</param>
    /// <param name="tamanhoPagina">Itens por página (default 50, máx. 200).</param>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<EmissaoLogResponse>>> Listar(
        [FromQuery] string? cnpj,
        [FromQuery] string? modelo,
        [FromQuery] string? serie,
        [FromQuery] string? ambiente,
        [FromQuery] string? status,
        [FromQuery] string? chave,
        [FromQuery(Name = "dataDe")] DateTime? dataDe,
        [FromQuery(Name = "dataAte")] DateTime? dataAte,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = EmissaoLogService.TamanhoPaginaDefault,
        CancellationToken ct = default)
    {
        var resultado = await _service.ListarAsync(
            cnpj, modelo, serie, ambiente, status, chave,
            dataDe, dataAte, pagina, tamanhoPagina, ct);

        return Ok(resultado);
    }

    /// <summary>Obtém o último registro de emissão por chave de acesso (44 dígitos).</summary>
    [HttpGet("{chave}")]
    public async Task<ActionResult<EmissaoLogResponse>> ObterPorChave(string chave, CancellationToken ct)
    {
        var item = await _service.ObterPorChaveAsync(chave, ct);
        return item is null ? NotFound() : Ok(item);
    }
}
