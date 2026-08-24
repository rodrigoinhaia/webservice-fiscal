using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FiscalService.Api.Config;

namespace FiscalService.Api.Services.Ibpt;

/// <summary>
/// Cliente HTTP da API De Olho no Imposto
/// (<see href="https://apidoni.ibpt.org.br/api/v1/produtos"/>).
/// </summary>
public sealed class IbptApiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;
    private readonly FiscalConfig _config;
    private readonly ILogger<IbptApiClient> _logger;

    public IbptApiClient(HttpClient http, FiscalConfig config, ILogger<IbptApiClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<IbptAliquota?> ConsultarProdutoAsync(
        IbptCredencial credencial,
        IbptConsultaChave chave,
        string descricao,
        string unidade,
        decimal valor,
        string gtin,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credencial.Token))
            return null;

        var url = MontarUrl(credencial, chave, descricao, unidade, valor, gtin);
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("IBPT API {Status} para NCM {Ncm}/{Uf}: {Body}",
                    (int)resp.StatusCode, chave.Ncm, chave.Uf, Truncar(body));
                return null;
            }

            var dto = JsonSerializer.Deserialize<IbptProdutoApiDto>(body, JsonOpts);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Codigo))
            {
                _logger.LogWarning("IBPT API retornou JSON inesperado para NCM {Ncm}: {Body}", chave.Ncm, Truncar(body));
                return null;
            }

            return dto.ParaAliquota(chave.Uf);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Falha ao consultar IBPT API NCM={Ncm} UF={Uf}", chave.Ncm, chave.Uf);
            return null;
        }
    }

    private string MontarUrl(
        IbptCredencial credencial,
        IbptConsultaChave chave,
        string descricao,
        string unidade,
        decimal valor,
        string gtin)
    {
        var builder = new UriBuilder(_config.Ibpt.UrlProdutos);
        var q = new List<string>
        {
            "token=" + Uri.EscapeDataString(credencial.Token),
            "cnpj=" + Uri.EscapeDataString(SomenteDigitos(credencial.Cnpj)),
            "codigo=" + Uri.EscapeDataString(chave.Ncm),
            "uf=" + Uri.EscapeDataString(chave.Uf),
            "ex=" + chave.Ex.ToString(CultureInfo.InvariantCulture),
            "descricao=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(descricao) ? "PRODUTO" : descricao),
            "unidadeMedida=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(unidade) ? "UN" : unidade),
            "valor=" + valor.ToString("0.##", CultureInfo.InvariantCulture),
            "gtin=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(gtin) ? "SEM GTIN" : gtin)
        };
        builder.Query = string.Join("&", q);
        return builder.Uri.ToString();
    }

    private static string SomenteDigitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    private static string Truncar(string? s) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= 300 ? s : s[..300];

    internal sealed class IbptProdutoApiDto
    {
        public string? Codigo { get; set; }
        public string? UF { get; set; }
        public int EX { get; set; }
        public string? Descricao { get; set; }
        public decimal Nacional { get; set; }
        public decimal Estadual { get; set; }
        public decimal Municipal { get; set; }
        public decimal Importado { get; set; }
        public string? VigenciaInicio { get; set; }
        public string? VigenciaFim { get; set; }
        public string? Chave { get; set; }
        public string? Versao { get; set; }
        public string? Fonte { get; set; }

        public IbptAliquota ParaAliquota(string ufFallback) => new()
        {
            Codigo = IbptTributoCalculator.NormalizarNcm(Codigo),
            Uf = string.IsNullOrWhiteSpace(UF) ? ufFallback : UF.Trim().ToUpperInvariant(),
            Ex = EX,
            Descricao = Descricao,
            Nacional = Nacional,
            Importado = Importado,
            Estadual = Estadual,
            Municipal = Municipal,
            Chave = Chave,
            Versao = Versao,
            Fonte = Fonte,
            Origem = "api"
        };
    }
}
