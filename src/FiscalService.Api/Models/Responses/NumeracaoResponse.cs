namespace FiscalService.Api.Models.Responses;

public class NumeracaoResponse
{
    public string Cnpj { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string Ambiente { get; set; } = "Homologacao";
    public int UltimoNumero { get; set; }
    public int ProximoNumero { get; set; }
    public bool Reservado { get; set; }
    public ErroResponse? Erro { get; set; }
}

public class NumeracaoItemResponse
{
    public string Cnpj { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string ModeloDescricao { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string Ambiente { get; set; } = "Homologacao";
    public int UltimoNumero { get; set; }
    public int ProximoNumero { get; set; }
    public DateTime UltimaAtualizacao { get; set; }
}

public class NumeracaoListaResponse
{
    public List<NumeracaoItemResponse> Itens { get; set; } = new();
    public int Total { get; set; }
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
