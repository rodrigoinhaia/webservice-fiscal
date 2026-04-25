using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/nfce")]
[Produces("application/json")]
public class NFCeController : ControllerBase
{
    private readonly NFCeService _nfceService;

    public NFCeController(NFCeService nfceService)
    {
        _nfceService = nfceService;
    }

    /// <summary>Emite uma NFC-e 4.0 (venda ao consumidor). CSC e IdCSC são obrigatórios.</summary>
    [HttpPost("emitir")]
    public async Task<IActionResult> Emitir([FromBody] NFCeEmitirRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfceService.EmitirAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Cancela uma NFC-e autorizada.</summary>
    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] NFeCancelarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfceService.CancelarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }
}
