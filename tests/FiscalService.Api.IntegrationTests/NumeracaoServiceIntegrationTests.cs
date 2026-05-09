using FiscalService.Api.Data;
using FiscalService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FiscalService.Api.IntegrationTests;

public sealed class NumeracaoServiceIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public NumeracaoServiceIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options);

    private static string RandomCnpj14()
    {
        Span<char> s = stackalloc char[14];
        for (var i = 0; i < 14; i++)
            s[i] = (char)('0' + Random.Shared.Next(0, 10));
        return new string(s);
    }

    [SkippableFact]
    public async Task ObterProximoNumeroAsync_primeira_reserva_retorna_1()
    {
        Skip.IfNot(_fixture.IsRunning, "Docker não disponível — teste de integração ignorado.");

        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var svc = new NumeracaoService(ctx, NullLogger<NumeracaoService>.Instance);
        var cnpj = RandomCnpj14();

        var n = await svc.ObterProximoNumeroAsync(cnpj, "55", "1");
        Assert.Equal(1, n);

        var consulta = await svc.ConsultarUltimoNumeroAsync(cnpj, "55", "1");
        Assert.Equal(1, consulta);
    }

    [SkippableFact]
    public async Task ObterProximoNumeroAsync_duas_reservas_incrementam()
    {
        Skip.IfNot(_fixture.IsRunning, "Docker não disponível — teste de integração ignorado.");

        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var svc = new NumeracaoService(ctx, NullLogger<NumeracaoService>.Instance);
        var cnpj = RandomCnpj14();

        Assert.Equal(1, await svc.ObterProximoNumeroAsync(cnpj, "55", "1"));
        Assert.Equal(2, await svc.ObterProximoNumeroAsync(cnpj, "55", "1"));
        Assert.Equal(2, await svc.ConsultarUltimoNumeroAsync(cnpj, "55", "1"));
    }

    [SkippableFact]
    public async Task ConfirmarNumeroAsync_ajusta_contador_quando_maior()
    {
        Skip.IfNot(_fixture.IsRunning, "Docker não disponível — teste de integração ignorado.");

        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var svc = new NumeracaoService(ctx, NullLogger<NumeracaoService>.Instance);
        var cnpj = RandomCnpj14();

        await svc.ObterProximoNumeroAsync(cnpj, "55", "1");
        await svc.ConfirmarNumeroAsync(cnpj, "55", "1", 50);

        Assert.Equal(50, await svc.ConsultarUltimoNumeroAsync(cnpj, "55", "1"));
        Assert.Equal(51, await svc.ObterProximoNumeroAsync(cnpj, "55", "1"));
    }
}
