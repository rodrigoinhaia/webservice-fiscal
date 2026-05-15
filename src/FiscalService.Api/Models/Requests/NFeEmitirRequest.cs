using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFeEmitirRequest : IEmitenteConfigSource
{
    /// <summary>CNPJ do emitente cadastrado em POST /api/emitentes (evita reenviar certificado/senha).</summary>
    public string? EmitenteCnpj { get; set; }

    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    public int NumeroNota { get; set; }
    public string Serie { get; set; } = "1";
    public string NaturezaOperacao { get; set; } = "Venda de Mercadoria";

    /// <summary>Finalidade: 1=Normal, 2=Complementar, 3=Ajuste, 4=Devolução.</summary>
    public int Finalidade { get; set; } = 1;

    /// <summary>Tipo de operação: 0=Entrada, 1=Saída.</summary>
    public int TipoOperacao { get; set; } = 1;

    /// <summary>Indicador de destinatário: 1=Operação interna, 2=Operação interestadual, 3=Operação exterior.</summary>
    public int IndicadorDestinatario { get; set; } = 1;

    [Required]
    public DestinatarioRequest Destinatario { get; set; } = null!;

    [Required]
    public List<ItemNFeRequest> Itens { get; set; } = new();

    public List<PagamentoRequest> Pagamentos { get; set; } = new();

    public string? InformacoesAdicionais { get; set; }

    /// <summary>Modalidade de frete: 0=Por conta do emitente, 1=Por conta do destinatário, etc.</summary>
    public int ModalidadeFrete { get; set; } = 1;
}
