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
    /// Lista séries cadastradas com último e próximo número.
    /// Filtros opcionais: <c>cnpj</c>, <c>ambiente</c>.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? cnpj = null,
        [FromQuery] string? ambiente = null,
        CancellationToken ct = default)
    {
        var itens = await _numeracaoService.ListarAsync(cnpj, ambiente, ct);
        var lista = itens.Select(n => new NumeracaoItemResponse
        {
            Cnpj = n.Cnpj,
            Modelo = n.Modelo,
            ModeloDescricao = NumeracaoService.DescreverModelo(n.Modelo),
            Serie = n.Serie,
            Ambiente = n.Ambiente,
            UltimoNumero = n.UltimoNumero,
            ProximoNumero = n.UltimoNumero + 1,
            UltimaAtualizacao = n.UltimaAtualizacao
        }).ToList();

        return Ok(new NumeracaoListaResponse { Itens = lista, Total = lista.Count });
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
            var ultimoAntes = await _numeracaoService.ConsultarUltimoNumeroAsync(cnpj, modelo, serie, amb, ct);

            if (reservar)
            {
                var reservado = await _numeracaoService.ObterProximoNumeroAsync(cnpj, modelo, serie, amb, ct);
                return Ok(new NumeracaoResponse
                {
                    Cnpj = cnpj,
                    Modelo = modelo,
                    Serie = serie,
                    Ambiente = amb,
                    UltimoNumero = reservado,
                    ProximoNumero = reservado,
                    Reservado = true
                });
            }

            return Ok(new NumeracaoResponse
            {
                Cnpj = cnpj,
                Modelo = modelo,
                Serie = serie,
                Ambiente = amb,
                UltimoNumero = ultimoAntes,
                ProximoNumero = ultimoAntes + 1,
                Reservado = false
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
            var ultimo = await _numeracaoService.ConsultarUltimoNumeroAsync(
                request.Cnpj, request.Modelo, request.Serie, amb, ct);
            return Ok(new
            {
                sucesso = true,
                mensagem = $"Número {request.Numero} confirmado com sucesso ({amb}).",
                ambiente = amb,
                ultimoNumero = ultimo,
                proximoNumero = ultimo + 1
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { sucesso = false, erro = new ErroResponse { Tipo = "ErroInterno", Mensagem = ex.Message } });
        }
    }
}
