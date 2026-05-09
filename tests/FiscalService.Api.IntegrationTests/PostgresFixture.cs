using Testcontainers.PostgreSql;
using Xunit;

namespace FiscalService.Api.IntegrationTests;

/// <summary>
/// Um container PostgreSQL 16 por classe de teste (IClassFixture).
/// Se o Docker não estiver acessível, <see cref="IsRunning"/> fica false e os testes podem usar <c>Skip.IfNot</c>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>True após <see cref="InitializeAsync"/> subir o container com sucesso.</summary>
    public bool IsRunning { get; private set; }

    public string ConnectionString =>
        IsRunning && _container is not null
            ? _container.GetConnectionString()
            : throw new InvalidOperationException("PostgreSQL de teste não iniciado (Docker indisponível ou falha ao subir o container).");

    public async Task InitializeAsync()
    {
        if (!DockerHostGuard.IsDockerReachable())
            return;

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();
        IsRunning = true;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
        _container = null;
        IsRunning = false;
    }
}
