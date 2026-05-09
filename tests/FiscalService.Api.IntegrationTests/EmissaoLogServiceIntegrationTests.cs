using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FiscalService.Api.IntegrationTests;

public sealed class EmissaoLogServiceIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EmissaoLogServiceIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

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
    public async Task Listar_retorna_pagina_filtrada_por_cnpj_e_modelo()
    {
        Skip.IfNot(_fixture.IsRunning, "Docker não disponível.");

        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var cnpj = RandomCnpj14();
        for (var i = 1; i <= 5; i++)
        {
            ctx.EmissaoLogs.Add(new EmissaoLog
            {
                Cnpj = cnpj,
                Modelo = "55",
                Serie = "1",
                Numero = i,
                ChaveAcesso = $"NFE{i:D41}",
                Status = "Autorizado",
                Ambiente = "Homologacao",
                DataEmissao = DateTime.UtcNow.AddMinutes(-i),
                DataProcessamento = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        ctx.EmissaoLogs.Add(new EmissaoLog
        {
            Cnpj = cnpj,
            Modelo = "65",
            Serie = "1",
            Numero = 99,
            ChaveAcesso = $"NFCE{99:D40}",
            Status = "Autorizado",
            Ambiente = "Homologacao",
            DataEmissao = DateTime.UtcNow,
            DataProcessamento = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = new EmissaoLogService(ctx);

        var pagina1 = await svc.ListarAsync(cnpj, "55", null, null, null, null, null, null, 1, 3);
        Assert.Equal(5, pagina1.Total);
        Assert.Equal(3, pagina1.Itens.Count);
        Assert.True(pagina1.TemProxima);
        Assert.Equal(2, pagina1.TotalPaginas);

        var pagina2 = await svc.ListarAsync(cnpj, "55", null, null, null, null, null, null, 2, 3);
        Assert.Equal(2, pagina2.Itens.Count);
        Assert.False(pagina2.TemProxima);
    }

    [SkippableFact]
    public async Task ObterPorChave_retorna_o_mais_recente()
    {
        Skip.IfNot(_fixture.IsRunning, "Docker não disponível.");

        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var chave = new string('7', 44);
        ctx.EmissaoLogs.Add(new EmissaoLog
        {
            Cnpj = RandomCnpj14(),
            Modelo = "55",
            Serie = "1",
            Numero = 1,
            ChaveAcesso = chave,
            Status = "Autorizado",
            Ambiente = "Homologacao",
            DataEmissao = DateTime.UtcNow.AddMinutes(-10),
            DataProcessamento = DateTime.UtcNow.AddMinutes(-10)
        });
        ctx.EmissaoLogs.Add(new EmissaoLog
        {
            Cnpj = RandomCnpj14(),
            Modelo = "55",
            Serie = "1",
            Numero = 1,
            ChaveAcesso = chave,
            Status = "Cancelado",
            Ambiente = "Homologacao",
            DataEmissao = DateTime.UtcNow.AddMinutes(-1),
            DataProcessamento = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = new EmissaoLogService(ctx);
        var item = await svc.ObterPorChaveAsync(chave);

        Assert.NotNull(item);
        Assert.Equal("Cancelado", item!.Status);
    }
}
