using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/nfe")]
[Produces("application/json")]
public class NFeController : ControllerBase
{
    private readonly NFeService _nfeService;

    public NFeController(NFeService nfeService)
    {
        _nfeService = nfeService;
    }

    /// <summary>Emite uma NF-e 4.0.</summary>
    [HttpPost("emitir")]
    public async Task<IActionResult> Emitir([FromBody] NFeEmitirRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfeService.EmitirAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Cancela uma NF-e autorizada.</summary>
    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] NFeCancelarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfeService.CancelarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Emite uma Carta de Correção Eletrônica (CC-e).</summary>
    [HttpPost("carta-correcao")]
    public async Task<IActionResult> CartaCorrecao([FromBody] NFeCartaCorrecaoRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfeService.CartaCorrecaoAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Consulta a situação de uma NF-e na SEFAZ.</summary>
    [HttpPost("consultar")]
    public async Task<IActionResult> Consultar([FromBody] NFeConsultarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfeService.ConsultarAsync(request, ct);
        return Ok(resultado);
    }

    /// <summary>Inutiliza uma faixa de numeração de NF-e.</summary>
    [HttpPost("inutilizar")]
    public async Task<IActionResult> Inutilizar([FromBody] NFeInutilizarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfeService.InutilizarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Consulta o status do serviço SEFAZ para NF-e.</summary>
    [HttpGet("status-sefaz")]
    public IActionResult StatusSefaz(
        [FromQuery] string uf,
        [FromQuery] string ambiente,
        [FromQuery] string certificadoPath,
        [FromQuery] string certificadoSenha,
        [FromQuery] string cnpj,
        [FromQuery] string razaoSocial = "Consulta")
    {
        var emitente = new ConfiguracaoEmitenteRequest
        {
            Uf = uf,
            Ambiente = ambiente,
            CertificadoPath = certificadoPath,
            CertificadoSenha = certificadoSenha,
            Cnpj = cnpj,
            RazaoSocial = razaoSocial
        };
        var resultado = _nfeService.ConsultarStatusSefaz(emitente);
        return Ok(resultado);
    }
}
