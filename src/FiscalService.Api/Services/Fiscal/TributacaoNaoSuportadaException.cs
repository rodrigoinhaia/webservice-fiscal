namespace FiscalService.Api.Services.Fiscal;

public sealed class TributacaoNaoSuportadaException : Exception
{
  public TributacaoNaoSuportadaException(string message) : base(message) { }
}
