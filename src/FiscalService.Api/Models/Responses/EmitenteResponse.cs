namespace FiscalService.Api.Models.Responses;

public class EmitenteResponse
{
    public long Id { get; set; }
    public string Cnpj { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }
    public string? Ie { get; set; }
    public int Crt { get; set; }
    public string Uf { get; set; } = string.Empty;
    public string Ambiente { get; set; } = string.Empty;
    public string CertificadoPath { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public EnderecoEmitenteResponse? Endereco { get; set; }
}

public class EnderecoEmitenteResponse
{
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Municipio { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? Cep { get; set; }
    public string? Telefone { get; set; }
}

public class EmitenteListaResponse
{
    public List<EmitenteResponse> Itens { get; set; } = new();
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
    public int Total { get; set; }
}
