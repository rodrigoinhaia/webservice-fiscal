using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/danfe")]
[Produces("application/json")]
public class DanfeController : ControllerBase
{
    private readonly DanfeService _danfeService;

    public DanfeController(DanfeService danfeService)
    {
        _danfeService = danfeService;
    }

    /// <summary>Gera o DANFE (PDF em base64) de uma NF-e a partir do XML do nfeProc.</summary>
    [HttpPost("nfe")]
    public IActionResult GerarNFe([FromBody] DanfeNFeRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = _danfeService.GerarNFePdfResponse(request.XmlNfeProc);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Gera o DANFE NFC-e (cupom fiscal em PDF base64) a partir do XML do nfeProc.</summary>
    [HttpPost("nfce")]
    public IActionResult GerarNFCe([FromBody] DanfeNFCeRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = _danfeService.GerarNFCePdfResponse(request.XmlNfeProc, request.IdCsc, request.Csc);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }
}
