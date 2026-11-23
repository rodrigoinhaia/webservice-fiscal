using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FiscalService.Api.IntegrationTests;

/// <summary>
/// Testes de integração de auditoria contra os containers oficiais da Receita Federal (Gabarito RTC).
/// Executam quando o docker-compose.gabarito.yml estiver ativo no Docker Desktop.
/// </summary>
public sealed class RfbGabaritoIntegrationTests
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private const string DanfseUrl = "http://localhost:8085";
    private const string RegularUrl = "http://localhost:8086";
    private const string SimplesUrl = "http://localhost:8087";

    private static async Task<bool> IsServiceAvailableAsync(string url)
    {
        try
        {
            var res = await Http.GetAsync(url);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [SkippableFact]
    public async Task Gabarito_Danfse_Container_DeveEstarOnlineEPronto()
    {
        var online = await IsServiceAvailableAsync($"{DanfseUrl}/health/live");
        Skip.If(!online, "Container rfb-danfse (porta 8085) não está ativo.");

        var response = await Http.GetAsync($"{DanfseUrl}/health/live");
        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("ok", status);
    }

    [SkippableFact]
    public async Task Gabarito_CalculadoraRegular_DeveCalcularIbsCbsComSucesso()
    {
        var online = await IsServiceAvailableAsync($"{RegularUrl}/api/swagger-ui.html");
        Skip.If(!online, "Container rfb-calculadora-regular (porta 8086) não está ativo.");

        var payload = new
        {
            id = "test-gabarito-001",
            versao = "1.0.0",
            dhFatoGerador = "2027-03-10T10:00:00-03:00",
            municipio = 4314902,
            uf = "RS",
            itens = new[]
            {
                new
                {
                    numero = 1,
                    ncm = "10063021",
                    cst = "200",
                    cClassTrib = "200003",
                    baseCalculo = 100.00m,
                    quantidade = 20,
                    unidade = "KG",
                    aliquotasNominais = new
                    {
                        cbs = 8.8m,
                        ibsEstadual = 0.05m,
                        ibsMunicipal = 0.05m
                    }
                }
            }
        };

        var response = await Http.PostAsJsonAsync($"{RegularUrl}/api/calculadora/regime-geral", payload);
        Assert.True(response.IsSuccessStatusCode, $"Falha na chamada da calculadora regular: {response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("objetos", out var objetos));
        Assert.Equal(1, objetos.GetArrayLength());

        var primeiroItem = objetos[0];
        Assert.True(primeiroItem.TryGetProperty("tribCalc", out var tribCalc));
        Assert.True(tribCalc.TryGetProperty("IBSCBS", out var ibscbs));
        Assert.Equal("200", ibscbs.GetProperty("CST").GetString());
        Assert.Equal("200003", ibscbs.GetProperty("cClassTrib").GetString());
    }

    [SkippableFact]
    public async Task Gabarito_CalculadoraSimples_DeveRetornarVersaoEBaseDeDados()
    {
        var online = await IsServiceAvailableAsync($"{SimplesUrl}/api/swagger-ui.html");
        Skip.If(!online, "Container rfb-calculadora-simples (porta 8087) não está ativo.");

        var response = await Http.GetAsync($"{SimplesUrl}/api/versao");
        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("versaoApp", out var versaoApp));
        Assert.False(string.IsNullOrWhiteSpace(versaoApp.GetString()));

        Assert.True(root.TryGetProperty("versaoDb", out var versaoDb));
        Assert.False(string.IsNullOrWhiteSpace(versaoDb.GetString()));
    }
}
