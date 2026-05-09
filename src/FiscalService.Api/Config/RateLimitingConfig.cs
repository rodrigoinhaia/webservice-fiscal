namespace FiscalService.Api.Config;

/// <summary>Limitação global por IP (janela fixa). <c>/health</c> fica sem limite.</summary>
public sealed class RateLimitingConfig
{
    public const string SectionName = "RateLimiting";

    /// <summary>Desliga o limitador (útil em testes locais).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Máximo de requisições por IP por janela (exceto health).</summary>
    public int PermitLimit { get; set; } = 180;

    /// <summary>Duração da janela em segundos.</summary>
    public int WindowSeconds { get; set; } = 60;
}
