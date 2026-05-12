namespace FiscalService.Api.Models.Responses;

/// <summary>Resultado da geração de DANFE em HTML (impressão / PDF pelo navegador).</summary>
public sealed class DanfeHtmlResponse
{
    public bool Sucesso { get; set; }
    /// <summary>Documento HTML completo (UTF-8). Preenchido quando <see cref="Sucesso"/> é true.</summary>
    public string? Html { get; set; }
    public ErroResponse? Erro { get; set; }
}
