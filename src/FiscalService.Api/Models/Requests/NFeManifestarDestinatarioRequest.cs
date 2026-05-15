using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFeManifestarDestinatarioRequest : IEmitenteConfigSource
{
    public string? EmitenteCnpj { get; set; }
    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    [Required]
    [StringLength(44, MinimumLength = 44)]
    public string ChaveAcesso { get; set; } = string.Empty;

    /// <summary>Ciencia (210210), Confirmacao (210200), Desconhecimento (210220), NaoRealizada (210240) ou código numérico.</summary>
    [Required]
    public string TipoManifestacao { get; set; } = "Ciencia";

    /// <summary>Obrigatória para NaoRealizada (210240) — mínimo 15 caracteres.</summary>
    public string? Justificativa { get; set; }

    public int SequenciaEvento { get; set; } = 1;

    /// <summary>CNPJ/CPF do destinatário manifestante. Se omitido, usa o CNPJ do emitente.</summary>
    public string? DocumentoManifestante { get; set; }
}
