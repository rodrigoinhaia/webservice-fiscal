using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NumeracaoConfirmarRequest
{
    [Required]
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Modelo do documento: "55", "65", "57", "58".</summary>
    [Required]
    public string Modelo { get; set; } = string.Empty;

    [Required]
    public string Serie { get; set; } = string.Empty;

    public int Numero { get; set; }
}

public class StatusServicoRequest
{
    [Required]
    public ConfiguracaoEmitenteRequest ConfiguracaoEmitente { get; set; } = null!;

    /// <summary>Modelo: "NFe", "NFCe", "CTe", "MDFe".</summary>
    public string Modelo { get; set; } = "NFe";
}
