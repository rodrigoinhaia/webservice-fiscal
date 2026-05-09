namespace FiscalService.Api.Configuration;

/// <summary>
/// Aceita uma ou mais chaves na configuração (<c>ApiKey</c>), separadas por vírgula, pipe ou ponto-e-vírgula,
/// para rotação sem downtime. Comparação ordinal (sensível a maiúsculas).
/// </summary>
public static class ApiKeyRing
{
    private static readonly char[] Separators = { ',', '|', ';', '\n', '\r' };

    public static bool Matches(string? configuredKeys, string provided)
    {
        if (string.IsNullOrWhiteSpace(configuredKeys))
            return false;

        foreach (var segment in configuredKeys.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var k = segment.Trim();
            if (k.Length > 0 && string.Equals(k, provided, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
