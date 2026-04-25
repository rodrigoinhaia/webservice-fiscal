using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFeCancelarRequest
{
    [Required]
    public ConfiguracaoEmitenteRequest ConfiguracaoEmitente { get; set; } = null!;

    [Required]
    [StringLength(44, MinimumLength = 44)]
    public string ChaveAcesso { get; set; } = string.Empty;

    [Required]
    public string Protocolo { get; set; } = string.Empty;

    /// <summary>Justificativa de cancelamento (mínimo 15 caracteres).</summary>
    [Required]
    [MinLength(15)]
    public string Justificativa { get; set; } = string.Empty;
}
