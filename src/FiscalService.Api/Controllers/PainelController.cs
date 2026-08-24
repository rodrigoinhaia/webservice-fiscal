using Microsoft.AspNetCore.Mvc;

namespace FiscalService.Api.Controllers;

/// <summary>Painel operacional (HTML) para token IBPT e upload da tabela.</summary>
[ApiExplorerSettings(IgnoreApi = true)]
public class PainelController : ControllerBase
{
    [HttpGet("/painel")]
    [HttpGet("/painel/")]
    [HttpGet("/painel/index.html")]
    public IActionResult Index()
    {
        var candidatos = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "painel", "index.html"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "painel", "index.html"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "FiscalService.Api", "wwwroot", "painel", "index.html")
        };

        var path = candidatos.FirstOrDefault(System.IO.File.Exists);
        if (path is null)
            return NotFound("Painel IBPT não encontrado no deploy (wwwroot/painel/index.html).");

        return PhysicalFile(path, "text/html; charset=utf-8");
    }
}
