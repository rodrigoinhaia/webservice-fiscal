using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFSeCancelarRequest : IEmitenteConfigSource
{
    public string? EmitenteCnpj { get; set; }
    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 50)]
    public string ChaveAcesso { get; set; } = string.Empty;

    /// <summary>Código do motivo: ErroEmissao, ServicoNaoPrestado, etc.</summary>
    public string CodigoMotivo { get; set; } = "ErroEmissao";

    [Required]
    [MinLength(15)]
    public string DescricaoMotivo { get; set; } = string.Empty;
}
