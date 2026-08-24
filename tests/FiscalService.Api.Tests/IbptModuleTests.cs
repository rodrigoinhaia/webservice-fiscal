using FiscalService.Api.Config;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services.Ibpt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FiscalService.Api.Tests;

public class IbptTributoCalculatorTests
{
    [Theory]
    [InlineData("0", false)]
    [InlineData("3", false)]
    [InlineData("4", false)]
    [InlineData("5", false)]
    [InlineData("8", false)]
    [InlineData("1", true)]
    [InlineData("2", true)]
    [InlineData("6", true)]
    [InlineData("7", true)]
    public void Origem_importada_conforme_tabela_nfe(string origem, bool esperado)
    {
        Assert.Equal(esperado, IbptTributoCalculator.OrigemImportada(origem));
    }

    [Fact]
    public void Calcula_federal_nacional_mais_estadual_e_municipal()
    {
        var item = new ItemNFeRequest
        {
            ValorTotalBruto = 100m,
            OrigemMercadoria = "0"
        };
        var aliq = new IbptAliquota
        {
            Nacional = 13.45m,
            Importado = 20m,
            Estadual = 18m,
            Municipal = 0m
        };

        var r = IbptTributoCalculator.CalcularItem(item, aliq);

        Assert.False(r.Importado);
        Assert.Equal(13.45m, r.Federal);
        Assert.Equal(18.00m, r.Estadual);
        Assert.Equal(0m, r.Municipal);
        Assert.Equal(31.45m, r.Total);
    }

    [Fact]
    public void Origem_importada_usa_aliquota_federal_de_importados()
    {
        var item = new ItemNFeRequest { ValorTotalBruto = 200m, OrigemMercadoria = "1" };
        var aliq = new IbptAliquota { Nacional = 10m, Importado = 15m, Estadual = 12m, Municipal = 2m };

        var r = IbptTributoCalculator.CalcularItem(item, aliq);

        Assert.True(r.Importado);
        Assert.Equal(30.00m, r.Federal);
        Assert.Equal(24.00m, r.Estadual);
        Assert.Equal(4.00m, r.Municipal);
        Assert.Equal(58.00m, r.Total);
    }

    [Fact]
    public void InfCpl_segue_lei_12741_com_fonte_ibpt()
    {
        var texto = IbptTributoCalculator.MontarInfCpl(1.23m, 4.56m, 0m, "IBPT/Empresometro", "26.2.A");

        Assert.Contains("Lei n° 12.741/2012", texto);
        Assert.Contains("Federais:", texto);
        Assert.Contains("Estaduais:", texto);
        Assert.Contains("Municipais:", texto);
        Assert.Contains("Fonte: IBPT/Empresometro/26.2.A", texto);
    }

    [Fact]
    public void Combinar_infCpl_nao_duplica_quando_ja_existe_lei()
    {
        var existente = "Totais aproximados dos Tributos cfe. Lei n° 12.741/2012: Federais: R$ 1,00";
        var r = IbptTributoCalculator.CombinarInfCpl(existente, "outro");
        Assert.Equal(existente, r);
    }

    [Fact]
    public void Combinar_infCpl_anexa_ao_texto_do_erp()
    {
        var r = IbptTributoCalculator.CombinarInfCpl("Venda interna.", "Totais aproximados...");
        Assert.Equal("Venda interna. Totais aproximados...", r);
    }
}

public class IbptTabelaParserTests
{
    [Fact]
    public void Parseia_csv_oficial_por_ponto_e_virgula()
    {
        const string csv =
            "codigo;ex;tipo;descricao;nacionalfederal;importadosfederal;estadual;municipal;vigenciainicio;vigenciafim;chave;versao;fonte\n" +
            "19059090;0;0;PAO;13,45;18,00;18,00;0,00;20/08/2026;30/09/2026;ABC;26.2.A;IBPT/Empresometro\n" +
            "1234;0;1;SERVICO NBS;1;1;1;1;20/08/2026;30/09/2026;ABC;26.2.A;IBPT\n";

        using var reader = new StringReader(csv);
        var lista = IbptTabelaParser.Parse(reader, "RS");

        var item = Assert.Single(lista);
        Assert.Equal("19059090", item.Codigo);
        Assert.Equal("RS", item.Uf);
        Assert.Equal(13.45m, item.Nacional);
        Assert.Equal(18.00m, item.Importado);
        Assert.Equal(18.00m, item.Estadual);
        Assert.Equal("26.2.A", item.Versao);
        Assert.Equal("tabela", item.Origem);
    }
}

public class IbptTributoServiceTests
{
    [Fact]
    public async Task Preenche_vTotTrib_e_infCpl_quando_lookup_retorna_aliquota()
    {
        var lookup = new FakeLookup
        {
            Aliquota = new IbptAliquota
            {
                Codigo = "19059090",
                Uf = "RS",
                Nacional = 10m,
                Importado = 20m,
                Estadual = 18m,
                Municipal = 0m,
                Fonte = "IBPT",
                Versao = "26.2.A"
            }
        };
        var cfg = new FiscalConfig { Ibpt = new IbptConfig { Habilitado = true, Token = "x", IncluirInfCpl = true } };
        var svc = new IbptTributoService(cfg, lookup, NullLogger<IbptTributoService>.Instance);
        var itens = new List<ItemNFeRequest>
        {
            new()
            {
                Ncm = "19059090",
                ValorTotalBruto = 100m,
                OrigemMercadoria = "0",
                DescricaoProduto = "Pao",
                UnidadeComercial = "UN"
            }
        };

        var r = await svc.AplicarAsync(new ConfiguracaoEmitenteRequest { Cnpj = "123", Uf = "RS" }, itens, true, default);

        Assert.True(r.Aplicado);
        Assert.Equal(10m, r.Federal);
        Assert.Equal(18m, r.Estadual);
        Assert.Equal(28m, r.Total);
        Assert.Equal(28m, itens[0].ValorAproximadoTributos);
        Assert.Contains("12.741/2012", r.InfCpl);
        Assert.Contains("Fonte: IBPT/26.2.A", r.InfCpl);
    }

    [Fact]
    public async Task Nao_falha_quando_desabilitado()
    {
        var cfg = new FiscalConfig { Ibpt = new IbptConfig { Habilitado = true } };
        var svc = new IbptTributoService(cfg, new FakeLookup(), NullLogger<IbptTributoService>.Instance);
        var itens = new List<ItemNFeRequest> { new() { Ncm = "19059090", ValorTotalBruto = 10 } };

        var r = await svc.AplicarAsync(new ConfiguracaoEmitenteRequest { Cnpj = "1", Uf = "RS" }, itens, calcularOverride: false, default);

        Assert.False(r.Aplicado);
        Assert.Null(itens[0].ValorAproximadoTributos);
    }

    private sealed class FakeLookup : IIbptAliquotaLookup
    {
        public IbptAliquota? Aliquota { get; set; }

        public Task<IbptAliquota?> ObterAsync(
            IbptCredencial credencial,
            IbptConsultaChave chave,
            string descricao,
            string unidade,
            decimal valor,
            string gtin,
            CancellationToken ct) => Task.FromResult(Aliquota);
    }
}

public class IbptApiClientTests
{
    [Fact]
    public async Task Desserializa_json_da_api_produtos()
    {
        const string json = """
            {"Codigo":"19059090","UF":"RS","EX":0,"Descricao":"PAO","Nacional":13.45,"Estadual":18.0,"Municipal":0.0,"Importado":18.0,"Chave":"XYZ","Versao":"26.2.A","Fonte":"IBPT"}
            """;
        var handler = new StubHandler(json);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://apidoni.ibpt.org.br/") };
        var client = new IbptApiClient(
            http,
            new FiscalConfig { Ibpt = new IbptConfig { UrlProdutos = "https://apidoni.ibpt.org.br/api/v1/produtos" } },
            NullLogger<IbptApiClient>.Instance);

        var aliq = await client.ConsultarProdutoAsync(
            new IbptCredencial("00000000000000", "token"),
            new IbptConsultaChave("19059090", "RS"),
            "PAO", "UN", 10m, "SEM GTIN", default);

        Assert.NotNull(aliq);
        Assert.Equal("19059090", aliq!.Codigo);
        Assert.Equal(13.45m, aliq.Nacional);
        Assert.Equal("api", aliq.Origem);
        Assert.Contains("token=", handler.LastUrl);
        Assert.Contains("codigo=19059090", handler.LastUrl);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        public string LastUrl { get; private set; } = "";

        public StubHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri?.ToString() ?? "";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}

public class IbptTabelaArquivoStoreTests
{
    [Fact]
    public void Importar_grava_csv_e_permite_busca_por_ncm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ibpt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new FiscalConfig
            {
                Ibpt = new IbptConfig { Diretorio = dir, Habilitado = true }
            };
            var store = new IbptTabelaArquivoStore(cfg, new IbptCacheStamp(), NullLogger<IbptTabelaArquivoStore>.Instance);
            const string csv =
                "codigo;ex;tipo;descricao;nacionalfederal;importadosfederal;estadual;municipal;vigenciainicio;vigenciafim;chave;versao;fonte\n" +
                "19059090;0;0;PAO;13,45;18,00;18,00;0,00;20/08/2026;30/09/2026;ABC;26.2.A;IBPT\n";

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
            var r = store.Importar(stream, "RS");

            Assert.True(r.Sucesso);
            Assert.Equal(1, r.Registros);
            Assert.Equal("26.2.A", r.Versao);
            var aliq = store.Buscar("19059090", "RS", 0);
            Assert.NotNull(aliq);
            Assert.Equal(13.45m, aliq!.Nacional);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* temp */ }
        }
    }

    [Fact]
    public void Importar_rejeita_arquivo_sem_ncm()
    {
        var cfg = new FiscalConfig { Ibpt = new IbptConfig { Diretorio = Path.GetTempPath() } };
        var store = new IbptTabelaArquivoStore(cfg, new IbptCacheStamp(), NullLogger<IbptTabelaArquivoStore>.Instance);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("nao e csv"));
        var r = store.Importar(stream, "RS");
        Assert.False(r.Sucesso);
        Assert.Equal(0, r.Registros);
    }
}
