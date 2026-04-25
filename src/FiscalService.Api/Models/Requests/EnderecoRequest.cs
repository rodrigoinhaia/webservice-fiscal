namespace FiscalService.Api.Models.Requests;

public class EnderecoRequest
{
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Municipio { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }
    public string? Pais { get; set; } = "Brasil";
    public string? CodigoPais { get; set; } = "1058";
    public string? Telefone { get; set; }
}
