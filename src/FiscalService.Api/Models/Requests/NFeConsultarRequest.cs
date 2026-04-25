using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFeConsultarRequest
{
    [Required]
    public ConfiguracaoEmitenteRequest ConfiguracaoEmitente { get; set; } = null!;

    [Required]
    [StringLength(44, MinimumLength = 44)]
    public string ChaveAcesso { get; set; } = string.Empty;
}
