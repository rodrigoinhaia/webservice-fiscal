namespace FiscalService.Api.Models.Responses;

public sealed class IbptStatusResponse
{
    public bool Habilitado { get; init; }
    public bool PossuiTokenGlobal { get; init; }
    public bool TabelaCarregada { get; init; }
    public int TabelaRegistros { get; init; }
    public string? TabelaCaminho { get; init; }
    public string UrlProdutos { get; init; } = string.Empty;
    public bool IncluirInfCpl { get; init; }
    public bool Obrigatorio { get; init; }
    public string Observacao { get; init; } = string.Empty;
}

public sealed class IbptConsultaResponse
{
    public bool Encontrado { get; init; }
    public string? Ncm { get; init; }
    public string? Uf { get; init; }
    public int Ex { get; init; }
    public string? Descricao { get; init; }
    public decimal? AliquotaNacional { get; init; }
    public decimal? AliquotaImportado { get; init; }
    public decimal? AliquotaEstadual { get; init; }
    public decimal? AliquotaMunicipal { get; init; }
    public decimal? ValorFederal { get; init; }
    public decimal? ValorEstadual { get; init; }
    public decimal? ValorMunicipal { get; init; }
    public decimal? ValorTotal { get; init; }
    public bool? OrigemImportada { get; init; }
    public string? Fonte { get; init; }
    public string? Versao { get; init; }
    public string? Chave { get; init; }
    public string? OrigemDados { get; init; }
    public string? InfCpl { get; init; }
    public string? Aviso { get; init; }
}
