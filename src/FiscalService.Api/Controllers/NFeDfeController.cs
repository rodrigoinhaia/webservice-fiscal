using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/nfe")]
[Produces("application/json")]
public class NFeDfeController : ControllerBase
{
    private readonly NFeDfeService _dfeService;

    public NFeDfeController(NFeDfeService dfeService)
    {
        _dfeService = dfeService;
    }

    /// <summary>Consulta documentos via Distribuição DF-e (NSU, chave ou lote por ultNSU).</summary>
    [HttpPost("distribuicao-dfe")]
    public async Task<IActionResult> DistribuicaoDfe([FromBody] NFeDistribuicaoDfeRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _dfeService.DistribuirAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Manifestação do destinatário (ciência, confirmação, desconhecimento, operação não realizada).</summary>
    [HttpPost("manifestar-destinatario")]
    public async Task<IActionResult> ManifestarDestinatario(
        [FromBody] NFeManifestarDestinatarioRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _dfeService.ManifestarDestinatarioAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }
}
