namespace FiscalService.Api.Models.Responses;

/// <summary>Item de listagem do log de emissões fiscais (modelo achatado para o cliente).</summary>
public class EmissaoLogResponse
{
    public long Id { get; set; }
    public string Cnpj { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public int Numero { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? Protocolo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CodigoStatus { get; set; }
    public string? MensagemStatus { get; set; }
    public string Ambiente { get; set; } = string.Empty;
    public DateTime DataEmissao { get; set; }
    public DateTime DataProcessamento { get; set; }
}

/// <summary>Conteúdo XML autorizado de uma emissão (por chave de acesso).</summary>
public class EmissaoXmlResponse
{
    public bool Sucesso { get; set; }
    public string ChaveAcesso { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/xml";
    public string Xml { get; set; } = string.Empty;
}

/// <summary>Resposta paginada genérica para listagem.</summary>
public class PagedResponse<T>
{
    public IReadOnlyList<T> Itens { get; set; } = Array.Empty<T>();
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
    public long Total { get; set; }
    public int TotalPaginas => TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanhoPagina);
    public bool TemProxima => Pagina < TotalPaginas;
}
