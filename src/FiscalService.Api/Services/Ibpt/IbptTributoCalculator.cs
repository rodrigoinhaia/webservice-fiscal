using System.Globalization;
using FiscalService.Api.Models.Requests;

namespace FiscalService.Api.Services.Ibpt;

/// <summary>Cálculo puro da carga tributária aproximada (Lei 12.741/2012 / NT 2013.003).</summary>
public static class IbptTributoCalculator
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Origens 1, 2, 6 e 7 usam alíquota federal de importados.</summary>
    public static bool OrigemImportada(string? origemMercadoria)
    {
        var d = (origemMercadoria ?? "0").Trim();
        return d is "1" or "2" or "6" or "7";
    }

    public static decimal BaseCalculoItem(ItemNFeRequest item)
    {
        var baseItem = item.ValorTotalBruto;
        if (!item.IndicadorTotal)
            return 0;
        return decimal.Round(Math.Max(0, baseItem), 2, MidpointRounding.AwayFromZero);
    }

    public static IbptItemTributo CalcularItem(ItemNFeRequest item, IbptAliquota aliquota)
    {
        var baseCalc = BaseCalculoItem(item);
        var importado = OrigemImportada(item.OrigemMercadoria);
        var pFed = importado ? aliquota.Importado : aliquota.Nacional;

        var federal = Percentual(baseCalc, pFed);
        var estadual = Percentual(baseCalc, aliquota.Estadual);
        var municipal = Percentual(baseCalc, aliquota.Municipal);

        return new IbptItemTributo
        {
            BaseCalculo = baseCalc,
            Federal = federal,
            Estadual = estadual,
            Municipal = municipal,
            Total = federal + estadual + municipal,
            Importado = importado,
            Aliquota = aliquota
        };
    }

    public static decimal Percentual(decimal valor, decimal aliquotaPercentual) =>
        decimal.Round(valor * aliquotaPercentual / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Texto padrão de DANFE/NFC-e:
    /// <c>Totais aproximados dos Tributos cfe. Lei n° 12.741/2012: Federais: …; Estaduais: …; Municipais: …</c>
    /// </summary>
    public static string MontarInfCpl(decimal federal, decimal estadual, decimal municipal, string? fonte, string? versao)
    {
        var texto =
            "Totais aproximados dos Tributos cfe. Lei n° 12.741/2012: " +
            $"Federais: {Moeda(federal)}; Estaduais: {Moeda(estadual)}; Municipais: {Moeda(municipal)}";

        var origem = string.IsNullOrWhiteSpace(fonte) ? "IBPT" : fonte.Trim();
        if (!string.IsNullOrWhiteSpace(versao))
            origem += "/" + versao.Trim();

        return texto + $". Fonte: {origem}";
    }

    public static string? CombinarInfCpl(string? existente, string? ibpt)
    {
        if (string.IsNullOrWhiteSpace(ibpt))
            return string.IsNullOrWhiteSpace(existente) ? null : existente.Trim();

        if (string.IsNullOrWhiteSpace(existente))
            return ibpt;

        if (existente.Contains("12.741", StringComparison.OrdinalIgnoreCase))
            return existente.Trim();

        var combinado = existente.Trim() + " " + ibpt;
        return combinado.Length <= 5000 ? combinado : combinado[..5000];
    }

    public static string NormalizarNcm(string? ncm)
    {
        var d = new string((ncm ?? "").Where(char.IsDigit).ToArray());
        if (d.Length == 0)
            return string.Empty;
        return d.Length >= 8 ? d[..8] : d.PadRight(8, '0');
    }

    private static string Moeda(decimal valor) => valor.ToString("C", PtBr);
}
