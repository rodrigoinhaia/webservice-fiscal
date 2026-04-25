using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/cte")]
[Produces("application/json")]
public class CTeController : ControllerBase
{
    private readonly CTeService _cteService;

    public CTeController(CTeService cteService)
    {
        _cteService = cteService;
    }

    /// <summary>Emite um CT-e 4.0 (Conhecimento de Transporte Eletrônico).</summary>
    [HttpPost("emitir")]
    public async Task<IActionResult> Emitir([FromBody] CTeEmitirRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _cteService.EmitirAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Cancela um CT-e autorizado.</summary>
    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] NFeCancelarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _cteService.CancelarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }
}
