using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFeInutilizarRequest : IEmitenteConfigSource
{
    public string? EmitenteCnpj { get; set; }
    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    [Required]
    public string Serie { get; set; } = string.Empty;

    public int NumeroInicial { get; set; }
    public int NumeroFinal { get; set; }

    /// <summary>Justificativa (mínimo 15 caracteres).</summary>
    [Required]
    [MinLength(15)]
    public string Justificativa { get; set; } = string.Empty;
}
