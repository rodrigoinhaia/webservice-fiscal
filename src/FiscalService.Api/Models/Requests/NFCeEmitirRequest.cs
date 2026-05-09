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

    /// <summary>Versão do QR Code exigida pela UF: "1", "2" ou "3". Padrão "2" (NFe 4.00).</summary>
    public string QrCodeVersao { get; set; } = "2";

    public string? InformacoesAdicionais { get; set; }
}
