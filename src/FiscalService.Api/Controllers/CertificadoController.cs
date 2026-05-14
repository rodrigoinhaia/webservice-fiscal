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

    /// <summary>
    /// Upload do certificado A1 como arquivo (multipart). Campos: <c>arquivo</c> (.pfx/.p12), <c>senha</c>;
    /// opcional <c>nome</c> para o nome salvo (senão usa o nome do arquivo enviado).
    /// </summary>
    [HttpPost("upload-arquivo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadArquivo(
        IFormFile? arquivo,
        [FromForm] string? senha,
        [FromForm] string? nome,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new
            {
                sucesso = false,
                erro = new { tipo = "Validacao", mensagem = "Envie o campo de formulário \"arquivo\" com o .pfx ou .p12." }
            });
        }

        if (string.IsNullOrWhiteSpace(senha))
        {
            return BadRequest(new
            {
                sucesso = false,
                erro = new { tipo = "Validacao", mensagem = "A senha do certificado (campo \"senha\") é obrigatória." }
            });
        }

        var nomeArquivo = !string.IsNullOrWhiteSpace(nome)
            ? nome!.Trim()
            : (string.IsNullOrWhiteSpace(arquivo.FileName) ? "certificado.pfx" : arquivo.FileName);
        nomeArquivo = Path.GetFileName(nomeArquivo);
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            nomeArquivo = "certificado.pfx";

        await using var read = arquivo.OpenReadStream();
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();

        var resultado = _certService.UploadArquivoBinario(bytes, nomeArquivo, senha);
        return resultado.Sucesso ? Ok(resultado) : UnprocessableEntity(resultado);
    }
}
