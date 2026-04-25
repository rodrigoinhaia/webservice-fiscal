namespace FiscalService.Api.Models.Requests;

public class PagamentoRequest
{
    /// <summary>
    /// Forma de pagamento:
    /// 01=Dinheiro, 02=Cheque, 03=Cartão de Crédito, 04=Cartão de Débito,
    /// 05=Crédito Loja, 10=Vale Alimentação, 11=Vale Refeição,
    /// 12=Vale Presente, 13=Vale Combustível, 15=Boleto Bancário,
    /// 16=Depósito Bancário, 17=Pagamento Instantâneo (PIX),
    /// 18=Transferência Bancária, 19=Programa de Fidelidade, 90=Sem pagamento, 99=Outros.
    /// </summary>
    public string FormaPagamento { get; set; } = "01";

    public decimal ValorPagamento { get; set; }

    /// <summary>Preenchido apenas para cartão: bandeira da operadora.</summary>
    public string? BandeiraCartao { get; set; }

    /// <summary>CNPJ da credenciadora para pagamento em cartão.</summary>
    public string? CnpjCredenciadora { get; set; }

    /// <summary>Número de autorização da operação.</summary>
    public string? NumeroAutorizacao { get; set; }

    /// <summary>Tipo de integração: 1=Integrado, 2=Não integrado (para TEF).</summary>
    public int? TipoIntegracao { get; set; }
}
