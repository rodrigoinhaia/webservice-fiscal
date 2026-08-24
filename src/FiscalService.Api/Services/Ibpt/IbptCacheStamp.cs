namespace FiscalService.Api.Services.Ibpt;

/// <summary>Invalida o cache em memória quando a tabela IBPT é recarregada/importada.</summary>
public sealed class IbptCacheStamp
{
    private int _geracao = 1;

    public int Geracao => Volatile.Read(ref _geracao);

    public void Invalidar() => Interlocked.Increment(ref _geracao);
}
