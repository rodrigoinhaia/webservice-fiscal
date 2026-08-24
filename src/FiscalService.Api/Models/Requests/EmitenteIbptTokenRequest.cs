namespace FiscalService.Api.Models.Requests;

public sealed class EmitenteIbptTokenRequest
{
    /// <summary>Token De Olho no Imposto. String vazia remove o token cadastrado.</summary>
    public string? IbptToken { get; set; }
}
