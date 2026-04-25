using DFe.Classes.Entidades;

namespace FiscalService.Api.Helpers;

public static class UfHelper
{
    public static Estado MapearUf(string uf) => uf.ToUpperInvariant().Trim() switch
    {
        "AC" => Estado.AC,
        "AL" => Estado.AL,
        "AM" => Estado.AM,
        "AP" => Estado.AP,
        "BA" => Estado.BA,
        "CE" => Estado.CE,
        "DF" => Estado.DF,
        "ES" => Estado.ES,
        "GO" => Estado.GO,
        "MA" => Estado.MA,
        "MG" => Estado.MG,
        "MS" => Estado.MS,
        "MT" => Estado.MT,
        "PA" => Estado.PA,
        "PB" => Estado.PB,
        "PE" => Estado.PE,
        "PI" => Estado.PI,
        "PR" => Estado.PR,
        "RJ" => Estado.RJ,
        "RN" => Estado.RN,
        "RO" => Estado.RO,
        "RR" => Estado.RR,
        "RS" => Estado.RS,
        "SC" => Estado.SC,
        "SE" => Estado.SE,
        "SP" => Estado.SP,
        "TO" => Estado.TO,
        _ => throw new ArgumentException($"UF inválida ou não suportada: '{uf}'")
    };

    public static bool IsValid(string uf)
    {
        try { MapearUf(uf); return true; }
        catch { return false; }
    }
}
