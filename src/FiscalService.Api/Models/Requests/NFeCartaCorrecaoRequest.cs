using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFeCartaCorrecaoRequest : IEmitenteConfigSource
{
    public string? EmitenteCnpj { get; set; }
    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    [Required]
    [StringLength(44, MinimumLength = 44)]
    public string ChaveAcesso { get; set; } = string.Empty;

    /// <summary>Sequência do evento de CC-e (1 a 20).</summary>
    [Range(1, 20)]
    public int SequenciaEvento { get; set; } = 1;

    /// <summary>Texto da correção (mínimo 15 caracteres).</summary>
    [Required]
    [MinLength(15)]
    public string Correcao { get; set; } = string.Empty;
}
