using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class CertificadoValidarRequest
{
    [Required]
    public string CertificadoBase64 { get; set; } = string.Empty;

    [Required]
    public string Senha { get; set; } = string.Empty;
}

public class CertificadoUploadRequest
{
    /// <summary>Nome do arquivo (ex: empresa_00000000000000.pfx).</summary>
    [Required]
    public string Nome { get; set; } = string.Empty;

    [Required]
    public string ConteudoBase64 { get; set; } = string.Empty;

    [Required]
    public string Senha { get; set; } = string.Empty;
}
