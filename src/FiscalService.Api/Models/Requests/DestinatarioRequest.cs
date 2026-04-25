namespace FiscalService.Api.Models.Requests;

public class DestinatarioRequest
{
    public string? Cnpj { get; set; }
    public string? Cpf { get; set; }
    public string? RazaoSocial { get; set; }
    public string? NomeFantasia { get; set; }
    public string? Ie { get; set; }

    /// <summary>Indicador da IE: 1=Contribuinte ICMS, 2=Contribuinte isento, 9=Não contribuinte.</summary>
    public int IndicadorIe { get; set; } = 9;

    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public EnderecoRequest? Endereco { get; set; }
}
