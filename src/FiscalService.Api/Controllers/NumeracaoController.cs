using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/numeracao")]
[Produces("application/json")]
public class NumeracaoController : ControllerBase
{
    private readonly NumeracaoService _numeracaoService;

    public NumeracaoController(NumeracaoService numeracaoService)
    {
        _numeracaoService = numeracaoService;
    }

    /// <summary>Retorna o próximo número disponível para o CNPJ/modelo/série informados.</summary>
    [HttpGet("{cnpj}/{modelo}/{serie}")]
    public async Task<IActionResult> ObterProximo(string cnpj, string modelo, string serie, CancellationToken ct)
    {
        try
        {
            var proximo = await _numeracaoService.ObterProximoNumeroAsync(cnpj, modelo, serie, ct);
            return Ok(new NumeracaoResponse
            {
                Cnpj = cnpj,
                Modelo = modelo,
                Serie = serie,
                ProximoNumero = proximo
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new NumeracaoResponse
            {
                Cnpj = cnpj, Modelo = modelo, Serie = serie,
                Erro = new ErroResponse { Tipo = "ErroInterno", Mensagem = ex.Message, Timestamp = DateTime.UtcNow }
            });
        }
    }

    /// <summary>Confirma que um número foi efetivamente usado, atualizando o contador se necessário.</summary>
    [HttpPost("confirmar")]
    public async Task<IActionResult> Confirmar([FromBody] NumeracaoConfirmarRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            await _numeracaoService.ConfirmarNumeroAsync(request.Cnpj, request.Modelo, request.Serie, request.Numero, ct);
            return Ok(new { sucesso = true, mensagem = $"Número {request.Numero} confirmado com sucesso." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { sucesso = false, erro = new ErroResponse { Tipo = "ErroInterno", Mensagem = ex.Message } });
        }
    }
}
