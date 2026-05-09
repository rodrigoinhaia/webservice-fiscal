using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using FiscalService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/consulta")]
[Produces("application/json")]
public class ConsultaController : ControllerBase
{
    private readonly NFeService _nfeService;
    private readonly NFCeService _nfceService;
    private readonly CTeService _cteService;
    private readonly MDFeService _mdfeService;
    private readonly ILogger<ConsultaController> _logger;

    public ConsultaController(
        NFeService nfeService,
        NFCeService nfceService,
        CTeService cteService,
        MDFeService mdfeService,
        ILogger<ConsultaController> logger)
    {
        _nfeService = nfeService;
        _nfceService = nfceService;
        _cteService = cteService;
        _mdfeService = mdfeService;
        _logger = logger;
    }

    /// <summary>
    /// Consulta o status do serviço SEFAZ para o modelo informado
    /// (<c>NFe</c>, <c>NFCe</c>, <c>CTe</c> ou <c>MDFe</c>; default <c>NFe</c>).
    /// </summary>
    [HttpPost("status-servico")]
    public IActionResult StatusServico([FromBody] StatusServicoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var modelo = (request.Modelo ?? "NFe").Trim();
        var resultado = modelo.ToUpperInvariant() switch
        {
            "NFCE" or "65" => _nfceService.ConsultarStatusSefaz(request.ConfiguracaoEmitente),
            "CTE" or "57" => _cteService.ConsultarStatusSefaz(request.ConfiguracaoEmitente),
            "MDFE" or "58" => _mdfeService.ConsultarStatusSefaz(request.ConfiguracaoEmitente),
            "NFE" or "55" => _nfeService.ConsultarStatusSefaz(request.ConfiguracaoEmitente),
            _ => new StatusServicoResponse
            {
                Sucesso = false,
                Modelo = modelo,
                Mensagem = $"Modelo '{modelo}' não suportado. Use NFe, NFCe, CTe ou MDFe.",
                Erro = new ErroResponse
                {
                    Tipo = "ModeloInvalido",
                    Mensagem = "Modelo de documento desconhecido.",
                    Timestamp = DateTime.UtcNow
                }
            }
        };

        _logger.LogInformation("Status SEFAZ consultado: Modelo={Modelo} UF={UF} Sucesso={Sucesso}",
            resultado.Modelo, resultado.Uf, resultado.Sucesso);

        return Ok(resultado);
    }
}
