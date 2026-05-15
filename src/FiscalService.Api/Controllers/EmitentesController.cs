using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/emitentes")]
[Produces("application/json")]
public class EmitentesController : ControllerBase
{
    private readonly EmitenteService _emitenteService;

    public EmitentesController(EmitenteService emitenteService)
    {
        _emitenteService = emitenteService;
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] EmitenteCadastroRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var resultado = await _emitenteService.CriarAsync(request, ct);
            return CreatedAtAction(nameof(ObterPorCnpj), new { cnpj = resultado.Cnpj }, resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { sucesso = false, erro = new { tipo = "Validacao", mensagem = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { sucesso = false, erro = new { tipo = "CertificadoInvalido", mensagem = ex.Message } });
        }
    }

    [HttpGet("{cnpj}")]
    public async Task<IActionResult> ObterPorCnpj(string cnpj, CancellationToken ct)
    {
        var resultado = await _emitenteService.ObterPorCnpjAsync(cnpj, ct);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 50,
        [FromQuery] bool? ativo = true,
        CancellationToken ct = default)
    {
        return Ok(await _emitenteService.ListarAsync(pagina, tamanhoPagina, ativo, ct));
    }

    [HttpPut("{cnpj}")]
    public async Task<IActionResult> Atualizar(string cnpj, [FromBody] EmitenteAtualizarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var resultado = await _emitenteService.AtualizarAsync(cnpj, request, ct);
            return resultado is null ? NotFound() : Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { sucesso = false, erro = new { tipo = "CertificadoInvalido", mensagem = ex.Message } });
        }
    }

    [HttpDelete("{cnpj}")]
    public async Task<IActionResult> Desativar(string cnpj, CancellationToken ct)
    {
        var ok = await _emitenteService.DesativarAsync(cnpj, ct);
        return ok ? Ok(new { sucesso = true, mensagem = "Emitente desativado." }) : NotFound();
    }
}
