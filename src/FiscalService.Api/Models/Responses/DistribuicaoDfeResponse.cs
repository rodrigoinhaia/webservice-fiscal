namespace FiscalService.Api.Models.Responses;

public class DistribuicaoDfeResponse
{
    public bool Sucesso { get; set; }
    public string? CodigoStatus { get; set; }
    public string? Mensagem { get; set; }
    public string? UltNsu { get; set; }
    public string? MaxNsu { get; set; }
    public IReadOnlyList<DocumentoDistribuicaoDto> Documentos { get; set; } = Array.Empty<DocumentoDistribuicaoDto>();
    public string? XmlRetorno { get; set; }
    public ErroResponse? Erro { get; set; }
}

public class DocumentoDistribuicaoDto
{
    public string Nsu { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Xml { get; set; } = string.Empty;
}
