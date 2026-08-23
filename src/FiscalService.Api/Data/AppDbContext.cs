using FiscalService.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FiscalService.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<EmissaoLog> EmissaoLogs { get; set; } = null!;
    public DbSet<NumeracaoSequencial> NumeracoesSequenciais { get; set; } = null!;
    public DbSet<Emitente> Emitentes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NumeracaoSequencial>(entity =>
        {
            entity.HasIndex(n => new { n.Cnpj, n.Modelo, n.Serie, n.Ambiente })
                  .IsUnique()
                  .HasDatabaseName("ix_numeracoes_cnpj_modelo_serie_ambiente");
        });

        modelBuilder.Entity<EmissaoLog>(entity =>
        {
            // Índice por chave de acesso para consultas rápidas
            entity.HasIndex(e => e.ChaveAcesso)
                  .HasDatabaseName("ix_emissao_logs_chave_acesso");

            // Índice por CNPJ + data para relatórios e auditorias
            entity.HasIndex(e => new { e.Cnpj, e.DataEmissao })
                  .HasDatabaseName("ix_emissao_logs_cnpj_data");

            // Índice por status para dashboards operacionais
            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("ix_emissao_logs_status");
        });

        modelBuilder.Entity<Emitente>(entity =>
        {
            entity.HasIndex(e => e.Cnpj)
                  .IsUnique()
                  .HasDatabaseName("ix_emitentes_cnpj");
        });
    }
}
