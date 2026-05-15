using FiscalService.Api.Models.Requests;

namespace FiscalService.Api.Helpers;

internal static class NFeEmissaoPreconditions
{
    /// <summary>Código numérico (8 dígitos) para ide.cNF; o DFe.NET normaliza na <c>Assina</c>.</summary>
    public static string GerarCodigoNumericoNFe()
    {
        var n = Random.Shared.Next(1, 100_000_000);
        return n.ToString("D8");
    }

    /// <summary>enderEmit é obrigatório no XML; cadastro só expõe endereço quando há logradouro.</summary>
    public static void ValidarEnderecoEmitenteOuLancar(ConfiguracaoEmitenteRequest emitente)
    {
        ArgumentNullException.ThrowIfNull(emitente);
        var e = emitente.Endereco;
        if (e is null
            || string.IsNullOrWhiteSpace(e.Logradouro)
            || string.IsNullOrWhiteSpace(e.Numero)
            || string.IsNullOrWhiteSpace(e.Bairro)
            || string.IsNullOrWhiteSpace(e.CodigoMunicipio)
            || string.IsNullOrWhiteSpace(e.Municipio)
            || string.IsNullOrWhiteSpace(e.Uf))
        {
            throw new ArgumentException(
                "Cadastro do emitente sem endereço completo. Informe logradouro, número, bairro, município, código IBGE do município e UF (POST/PATCH /api/emitentes).");
        }
    }
}
