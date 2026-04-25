using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FiscalService.Api.Services;

public class NumeracaoService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NumeracaoService> _logger;

    public NumeracaoService(AppDbContext db, ILogger<NumeracaoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Reserva atomicamente o próximo número disponível usando SELECT FOR UPDATE (pessimistic lock PostgreSQL).
    /// Garante que chamadas concorrentes nunca produzam o mesmo número.
    /// </summary>
    public async Task<int> ObterProximoNumeroAsync(string cnpj, string modelo, string serie, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // SELECT FOR UPDATE garante exclusão mútua no PostgreSQL
            var numeracao = await _db.NumeracoesSequenciais
                .FromSqlRaw(
                    "SELECT * FROM numeracoes_sequenciais WHERE cnpj = {0} AND modelo = {1} AND serie = {2} FOR UPDATE",
                    cnpj, modelo, serie)
                .FirstOrDefaultAsync(ct);

            if (numeracao is null)
            {
                numeracao = new NumeracaoSequencial
                {
                    Cnpj = cnpj,
                    Modelo = modelo,
                    Serie = serie,
                    UltimoNumero = 1,
                    UltimaAtualizacao = DateTime.UtcNow
                };
                _db.NumeracoesSequenciais.Add(numeracao);
            }
            else
            {
                numeracao.UltimoNumero++;
                numeracao.UltimaAtualizacao = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Número reservado: CNPJ={CNPJ} Modelo={Modelo} Serie={Serie} Numero={Numero}",
                cnpj, modelo, serie, numeracao.UltimoNumero);

            return numeracao.UltimoNumero;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Retorna o último número usado sem reservar um novo.</summary>
    public async Task<int> ConsultarUltimoNumeroAsync(string cnpj, string modelo, string serie, CancellationToken ct = default)
    {
        var numeracao = await _db.NumeracoesSequenciais
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Cnpj == cnpj && n.Modelo == modelo && n.Serie == serie, ct);

        return numeracao?.UltimoNumero ?? 0;
    }

    /// <summary>Força o contador para um número específico (usado após inutilização ou correção manual).</summary>
    public async Task ConfirmarNumeroAsync(string cnpj, string modelo, string serie, int numero, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var numeracao = await _db.NumeracoesSequenciais
                .FromSqlRaw(
                    "SELECT * FROM numeracoes_sequenciais WHERE cnpj = {0} AND modelo = {1} AND serie = {2} FOR UPDATE",
                    cnpj, modelo, serie)
                .FirstOrDefaultAsync(ct);

            if (numeracao is null)
            {
                _db.NumeracoesSequenciais.Add(new NumeracaoSequencial
                {
                    Cnpj = cnpj,
                    Modelo = modelo,
                    Serie = serie,
                    UltimoNumero = numero,
                    UltimaAtualizacao = DateTime.UtcNow
                });
            }
            else if (numero > numeracao.UltimoNumero)
            {
                numeracao.UltimoNumero = numero;
                numeracao.UltimaAtualizacao = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
