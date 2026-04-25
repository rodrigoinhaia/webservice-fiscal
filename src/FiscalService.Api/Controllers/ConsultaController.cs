using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/consulta")]
[Produces("application/json")]
public class ConsultaController : ControllerBase
{
    private readonly NFeService _nfeService;

    public ConsultaController(NFeService nfeService)
    {
        _nfeService = nfeService;
    }

    /// <summary>Consulta o status do serviço SEFAZ para qualquer modelo fiscal.</summary>
    [HttpPost("status-servico")]
    public IActionResult StatusServico([FromBody] StatusServicoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Roteamento por modelo — por hora todos usam o serviço NF-e (modelo padrão)
        var resultado = _nfeService.ConsultarStatusSefaz(request.ConfiguracaoEmitente);
        return Ok(resultado);
    }
}
