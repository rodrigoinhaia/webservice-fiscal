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

    /// <summary>
    /// Próximo número para CNPJ/modelo/série/ambiente.
    /// Por padrão <c>reservar=true</c> (reserva atômica). Use <c>reservar=false</c> para apenas consultar.
    /// </summary>
    [HttpGet("{cnpj}/{modelo}/{serie}")]
    public async Task<IActionResult> ObterProximo(
        string cnpj,
        string modelo,
        string serie,
        [FromQuery] string? ambiente = null,
        [FromQuery] bool reservar = true,
        CancellationToken ct = default)
    {
        try
        {
            var amb = NumeracaoService.NormalizarAmbiente(ambiente);
            var proximo = reservar
                ? await _numeracaoService.ObterProximoNumeroAsync(cnpj, modelo, serie, amb, ct)
                : await _numeracaoService.ConsultarProximoNumeroAsync(cnpj, modelo, serie, amb, ct);

            return Ok(new NumeracaoResponse
            {
                Cnpj = cnpj,
                Modelo = modelo,
                Serie = serie,
                Ambiente = amb,
                ProximoNumero = proximo,
                Reservado = reservar
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new NumeracaoResponse
            {
                Cnpj = cnpj,
                Modelo = modelo,
                Serie = serie,
                Ambiente = NumeracaoService.NormalizarAmbiente(ambiente),
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
            var amb = NumeracaoService.NormalizarAmbiente(request.Ambiente);
            await _numeracaoService.ConfirmarNumeroAsync(
                request.Cnpj, request.Modelo, request.Serie, request.Numero, amb, ct);
            return Ok(new
            {
                sucesso = true,
                mensagem = $"Número {request.Numero} confirmado com sucesso ({amb}).",
                ambiente = amb
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { sucesso = false, erro = new ErroResponse { Tipo = "ErroInterno", Mensagem = ex.Message } });
        }
    }
}
