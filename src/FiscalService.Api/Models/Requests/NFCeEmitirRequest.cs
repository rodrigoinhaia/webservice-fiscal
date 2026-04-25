using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFCeEmitirRequest
{
    [Required]
    public ConfiguracaoEmitenteRequest ConfiguracaoEmitente { get; set; } = null!;

    public int NumeroNota { get; set; }
    public string Serie { get; set; } = "1";
    public string NaturezaOperacao { get; set; } = "Venda a Consumidor";

    public DestinatarioRequest? Destinatario { get; set; }

    [Required]
    public List<ItemNFeRequest> Itens { get; set; } = new();

    [Required]
    public List<PagamentoRequest> Pagamentos { get; set; } = new();

    /// <summary>Identificador do CSC (Código de Segurança do Contribuinte) — obrigatório NFC-e.</summary>
    [Required]
    public string IdCsc { get; set; } = string.Empty;

    /// <summary>Código de Segurança do Contribuinte — obrigatório NFC-e.</summary>
    [Required]
    public string Csc { get; set; } = string.Empty;

    public string? InformacoesAdicionais { get; set; }
}
