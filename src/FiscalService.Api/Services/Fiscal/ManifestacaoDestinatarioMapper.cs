using NFe.Classes.Servicos.Tipos;

namespace FiscalService.Api.Services.Fiscal;

public static class ManifestacaoDestinatarioMapper
{
    public static NFeTipoEvento Resolver(string tipoManifestacao)
    {
        if (string.IsNullOrWhiteSpace(tipoManifestacao))
            throw new ArgumentException("tipoManifestacao é obrigatório.");

        var t = tipoManifestacao.Trim().ToUpperInvariant();
        return t switch
        {
            "210200" or "CONFIRMACAO" or "CONFIRMAÇÃO" => NFeTipoEvento.TeMdConfirmacaoDaOperacao,
            "210210" or "CIENCIA" or "CIÊNCIA" => NFeTipoEvento.TeMdCienciaDaOperacao,
            "210220" or "DESCONHECIMENTO" => NFeTipoEvento.TeMdDesconhecimentoDaOperacao,
            "210240" or "NAOREALIZADA" or "NAO_REALIZADA" or "OPERACAO_NAO_REALIZADA"
                => NFeTipoEvento.TeMdOperacaoNaoRealizada,
            _ => throw new ArgumentException(
                "tipoManifestacao inválido. Use: Ciencia (210210), Confirmacao (210200), Desconhecimento (210220), NaoRealizada (210240) ou o código numérico.")
        };
    }

    public static bool ExigeJustificativa(NFeTipoEvento tipo) =>
        tipo == NFeTipoEvento.TeMdOperacaoNaoRealizada;
}
