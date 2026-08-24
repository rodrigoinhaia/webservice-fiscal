using FiscalService.Api.Config;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services.NFSe;
using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

[ApiController]
[Route("api/nfse")]
[Produces("application/json")]
public class NFSeController : ControllerBase
{
    private readonly NFSeService _nfseService;
    private readonly FiscalConfig _fiscalConfig;

    public NFSeController(NFSeService nfseService, FiscalConfig fiscalConfig)
    {
        _nfseService = nfseService;
        _fiscalConfig = fiscalConfig;
    }

    /// <summary>Emite NFS-e via DPS (Padrão Nacional / ADN).</summary>
    [HttpPost("emitir")]
    public async Task<IActionResult> Emitir([FromBody] NFSeEmitirRequest request, CancellationToken ct)
    {
        if (!ModuloHabilitado(out var desabilitado)) return desabilitado;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfseService.EmitirAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Cancela NFS-e autorizada (evento ADN).</summary>
    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] NFSeCancelarRequest request, CancellationToken ct)
    {
        if (!ModuloHabilitado(out var desabilitado)) return desabilitado;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfseService.CancelarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>Consulta situação da NFS-e pela chave de acesso (50 dígitos).</summary>
    [HttpPost("consultar")]
    public async Task<IActionResult> Consultar([FromBody] NFSeConsultarRequest request, CancellationToken ct)
    {
        if (!ModuloHabilitado(out var desabilitado)) return desabilitado;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resultado = await _nfseService.ConsultarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    /// <summary>DANFSe em PDF (base64): geração local NT 008 a partir do XML autorizado.</summary>
    [HttpGet("danfse/{chave}")]
    public async Task<IActionResult> Danfse(
        string chave,
        [FromQuery] string? emitenteCnpj,
        [FromQuery] string? ambiente,
        [FromQuery] string? certificadoPath,
        [FromQuery] string? certificadoSenha,
        [FromQuery] string? cnpj,
        [FromQuery] string? razaoSocial,
        CancellationToken ct)
    {
        if (!ModuloHabilitado(out var desabilitado)) return desabilitado;

        var source = new NFSeDanfseQuerySource
        {
            EmitenteCnpj = emitenteCnpj,
            ConfiguracaoEmitente = string.IsNullOrWhiteSpace(certificadoPath) ? null : new ConfiguracaoEmitenteRequest
            {
                Cnpj = cnpj ?? emitenteCnpj ?? string.Empty,
                RazaoSocial = razaoSocial ?? "Consulta DANFSe",
                Ambiente = ambiente ?? "Homologacao",
                CertificadoPath = certificadoPath!,
                CertificadoSenha = certificadoSenha ?? string.Empty,
                Uf = "RS"
            }
        };

        var resultado = await _nfseService.DownloadDanfseAsync(chave, source, ct);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }

    private bool ModuloHabilitado(out IActionResult? resultado)
    {
        if (_fiscalConfig.NFSe.Habilitado)
        {
            resultado = null;
            return true;
        }

        resultado = NotFound(new
        {
            sucesso = false,
            erro = new
            {
                tipo = "ModuloDesabilitado",
                mensagem = "Módulo NFS-e Padrão Nacional desabilitado (Fiscal:NFSe:Habilitado=false).",
                timestamp = DateTime.UtcNow
            }
        });
        return false;
    }

    private sealed class NFSeDanfseQuerySource : IEmitenteConfigSource
    {
        public string? EmitenteCnpj { get; init; }
        public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; init; }
    }
}
