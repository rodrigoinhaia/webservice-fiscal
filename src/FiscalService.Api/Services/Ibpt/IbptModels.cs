namespace FiscalService.Api.Services.Ibpt;

public sealed record IbptCredencial(string Cnpj, string Token);

public sealed record IbptConsultaChave(string Ncm, string Uf, int Ex = 0);

/// <summary>Alíquotas percentuais IBPT para um NCM/UF (Lei 12.741/2012).</summary>
public sealed record IbptAliquota
{
    public string Codigo { get; init; } = string.Empty;
    public string Uf { get; init; } = string.Empty;
    public int Ex { get; init; }
    public string? Descricao { get; init; }
    public decimal Nacional { get; init; }
    public decimal Importado { get; init; }
    public decimal Estadual { get; init; }
    public decimal Municipal { get; init; }
    public DateTime? VigenciaInicio { get; init; }
    public DateTime? VigenciaFim { get; init; }
    public string? Chave { get; init; }
    public string? Versao { get; init; }
    public string? Fonte { get; init; }
    public string Origem { get; init; } = "api";
}

public sealed class IbptItemTributo
{
    public decimal BaseCalculo { get; init; }
    public decimal Federal { get; init; }
    public decimal Estadual { get; init; }
    public decimal Municipal { get; init; }
    public decimal Total { get; init; }
    public bool Importado { get; init; }
    public IbptAliquota? Aliquota { get; init; }
}

public sealed class IbptNotaResultado
{
    public bool Aplicado { get; init; }
    public decimal Federal { get; init; }
    public decimal Estadual { get; init; }
    public decimal Municipal { get; init; }
    public decimal Total { get; init; }
    public string? Fonte { get; init; }
    public string? Versao { get; init; }
    public string? Chave { get; init; }
    public string? InfCpl { get; init; }
    public string? Aviso { get; init; }
    public int ItensCalculados { get; init; }
    public int ItensSemAliquota { get; init; }
}
