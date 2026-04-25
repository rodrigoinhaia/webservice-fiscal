using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/certificado")]
[Produces("application/json")]
public class CertificadoController : ControllerBase
{
    private readonly CertificadoService _certService;

    public CertificadoController(CertificadoService certService)
    {
        _certService = certService;
    }

    /// <summary>Valida um certificado A1 (.pfx) e retorna suas informações.</summary>
    [HttpPost("validar")]
    public IActionResult Validar([FromBody] CertificadoValidarRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = _certService.Validar(request);
        return resultado.Valido ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Faz upload de um certificado .pfx e o salva no diretório configurado.</summary>
    [HttpPost("upload")]
    public IActionResult Upload([FromBody] CertificadoUploadRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = _certService.Upload(request);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }
}
