using FiscalService.Api.Config;
using FiscalService.Api.Services.Fiscal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FiscalService.Api.Tests;

public class SefazRetryTests
{
    private static readonly FiscalConfig Config = new()
    {
        SefazRetryHabilitado = true,
        SefazRetryMaxTentativas = 3,
        SefazRetryIntervaloMs = 1
    };

    [Fact]
    public void Retenta_em_timeout_e_sucede_na_segunda()
    {
        var tentativas = 0;
        var resultado = SefazRetry.Execute(Config, NullLogger.Instance, "teste", () =>
        {
            tentativas++;
            if (tentativas < 2)
                throw new TimeoutException("The operation has timed out.");
            return "ok";
        });

        Assert.Equal("ok", resultado);
        Assert.Equal(2, tentativas);
    }

    [Fact]
    public void Nao_retenta_argument_exception()
    {
        var tentativas = 0;
        Assert.Throws<ArgumentException>(() =>
            SefazRetry.Execute<string>(Config, NullLogger.Instance, "teste", () =>
            {
                tentativas++;
                throw new ArgumentException("campo inválido");
                return "";
            }));
        Assert.Equal(1, tentativas);
    }

    [Theory]
    [InlineData(typeof(System.Net.WebException))]
    [InlineData(typeof(TimeoutException))]
    public void Deve_retentar_excecoes_transitorias(Type tipo)
    {
        var ex = (Exception)Activator.CreateInstance(tipo, "falha de rede")!;
        Assert.True(SefazRetry.DeveRetentar(ex));
    }
}
