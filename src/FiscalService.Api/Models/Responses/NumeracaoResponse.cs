namespace FiscalService.Api.Models.Responses;

public class NumeracaoResponse
{
    public string Cnpj { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public int ProximoNumero { get; set; }
    public ErroResponse? Erro { get; set; }
}

public class StatusServicoResponse
{
    public bool Sucesso { get; set; }
    public string? CodigoStatus { get; set; }
    public string? Mensagem { get; set; }
    public string? Uf { get; set; }
    public string? Modelo { get; set; }
    public string? Ambiente { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ErroResponse? Erro { get; set; }
}

public class DanfeResponse
{
    public bool Sucesso { get; set; }
    public string? PdfBase64 { get; set; }
    public ErroResponse? Erro { get; set; }
}
