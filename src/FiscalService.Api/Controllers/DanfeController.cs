using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/danfe")]
public class DanfeController : ControllerBase
{
    private readonly DanfeService _danfeService;

    public DanfeController(DanfeService danfeService)
    {
        _danfeService = danfeService;
    }

    /// <summary>Gera o DANFE (PDF em base64) de uma NF-e a partir do XML do nfeProc.</summary>
    [HttpPost("nfe")]
    [Produces("application/json")]
    public IActionResult GerarNFe([FromBody] DanfeNFeRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = _danfeService.GerarNFePdfResponse(request.XmlNfeProc);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Gera DANFE NF-e em HTML. Com <c>inline=true</c>, retorna <c>text/html</c> para abrir/imprimir no navegador.</summary>
    [HttpPost("nfe/html")]
    [Produces("application/json", "text/html")]
    public IActionResult GerarNFeHtml([FromBody] DanfeNFeRequest request, [FromQuery] bool inline = false)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = _danfeService.GerarNFeHtmlResponse(request.XmlNfeProc);
        if (!resultado.Sucesso)
            return UnprocessableEntity(resultado);
        if (inline)
            return Content(resultado.Html!, "text/html; charset=utf-8");
        return Ok(resultado);
    }

    /// <summary>Gera o DANFE NFC-e (cupom fiscal em PDF base64) a partir do XML do nfeProc.</summary>
    [HttpPost("nfce")]
    [Produces("application/json")]
    public IActionResult GerarNFCe([FromBody] DanfeNFCeRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = _danfeService.GerarNFCePdfResponse(request.XmlNfeProc, request.IdCsc, request.Csc);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Gera DANFE NFC-e em HTML. Com <c>inline=true</c>, retorna <c>text/html</c>.</summary>
    [HttpPost("nfce/html")]
    [Produces("application/json", "text/html")]
    public IActionResult GerarNFCeHtml([FromBody] DanfeNFCeRequest request, [FromQuery] bool inline = false)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = _danfeService.GerarNFCeHtmlResponse(request.XmlNfeProc, request.IdCsc, request.Csc);
        if (!resultado.Sucesso)
            return UnprocessableEntity(resultado);
        if (inline)
            return Content(resultado.Html!, "text/html; charset=utf-8");
        return Ok(resultado);
    }
}
