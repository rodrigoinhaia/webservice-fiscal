using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFCeEmitirRequest : IEmitenteConfigSource
{
    public string? EmitenteCnpj { get; set; }
    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    public int NumeroNota { get; set; }
    public string Serie { get; set; } = "1";
    public string NaturezaOperacao { get; set; } = "Venda a Consumidor";

    public DestinatarioRequest? Destinatario { get; set; }

    [Required]
    public List<ItemNFeRequest> Itens { get; set; } = new();

    [Required]
    public List<PagamentoRequest> Pagamentos { get; set; } = new();

    /// <summary>Identificador do CSC — obrigatório na emissão (request ou cadastro do emitente).</summary>
    public string IdCsc { get; set; } = string.Empty;

    /// <summary>CSC — obrigatório na emissão (request ou cadastro do emitente).</summary>
    public string Csc { get; set; } = string.Empty;

    /// <summary>Versão do QR Code exigida pela UF: "1", "2" ou "3". Padrão "2" (NFe 4.00).</summary>
    public string QrCodeVersao { get; set; } = "2";

    public string? InformacoesAdicionais { get; set; }
}
