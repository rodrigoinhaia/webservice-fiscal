using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFSeConsultarRequest : IEmitenteConfigSource
{
    public string? EmitenteCnpj { get; set; }
    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 50)]
    public string ChaveAcesso { get; set; } = string.Empty;
}
