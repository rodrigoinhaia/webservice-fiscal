using OpenAC.Net.NFSe.Nacional.Common.Model;

namespace FiscalService.Api.Services.NFSe;

internal static class NFSeOpenAcResponseHelper
{
    public static string FormatarErros(IEnumerable<MensagemProcessamento>? erros) =>
        erros is null || !erros.Any()
            ? "Operação NFS-e rejeitada sem detalhes."
            : string.Join("; ", erros.Select(e =>
                string.IsNullOrWhiteSpace(e.Codigo)
                    ? e.Descricao ?? e.Mensagem ?? "Erro"
                    : $"{e.Codigo}: {e.Descricao ?? e.Mensagem}"));

    public static string FormatarSucesso(string fallback, IEnumerable<MensagemProcessamento>? alertas = null)
    {
        if (alertas is not null && alertas.Any())
            return string.Join("; ", alertas.Select(a => a.Descricao ?? a.Mensagem ?? fallback));
        return fallback;
    }
}
