namespace FiscalService.Api.Models.Requests;

/// <summary>
/// Permite informar <see cref="EmitenteCnpj"/> (cadastro em <c>/api/emitentes</c>)
/// ou <see cref="ConfiguracaoEmitente"/> completo em cada chamada.
/// </summary>
public interface IEmitenteConfigSource
{
    string? EmitenteCnpj { get; }
    ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; }
}
