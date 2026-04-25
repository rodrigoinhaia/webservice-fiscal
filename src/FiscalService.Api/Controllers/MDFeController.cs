using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/mdfe")]
[Produces("application/json")]
public class MDFeController : ControllerBase
{
    private readonly MDFeService _mdfeService;

    public MDFeController(MDFeService mdfeService)
    {
        _mdfeService = mdfeService;
    }

    /// <summary>Emite um MDF-e 3.0 (Manifesto Eletrônico de Documentos Fiscais).</summary>
    [HttpPost("emitir")]
    public async Task<IActionResult> Emitir([FromBody] MDFeEmitirRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _mdfeService.EmitirAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Encerra um MDF-e autorizado.</summary>
    [HttpPost("encerrar")]
    public async Task<IActionResult> Encerrar([FromBody] MDFeEncerrarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _mdfeService.EncerrarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Cancela um MDF-e autorizado.</summary>
    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] MDFeCancelarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _mdfeService.CancelarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }
}
