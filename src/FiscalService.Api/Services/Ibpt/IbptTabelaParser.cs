using System.Globalization;

namespace FiscalService.Api.Services.Ibpt;

/// <summary>
/// Parser da tabela IBPT (CSV/TXT com <c>;</c>), no formato publicado em
/// <see href="https://deolhonoimposto.ibpt.org.br/"/>.
/// </summary>
public static class IbptTabelaParser
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<IbptAliquota> Parse(TextReader reader, string? ufPadrao = null)
    {
        var lista = new List<IbptAliquota>();
        string? versao = null;
        string? chave = null;
        string? fonte = null;
        DateTime? vigIni = null;
        DateTime? vigFim = null;

        string? linha;
        while ((linha = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(linha))
                continue;

            var cols = Split(linha);
            if (cols.Length == 0)
                continue;

            if (EhCabecalho(cols[0]))
                continue;

            if (EhMetadado(cols[0], cols))
            {
                CapturarMetadado(cols, ref versao, ref chave, ref fonte, ref vigIni, ref vigFim);
                continue;
            }

            if (cols.Length < 8)
                continue;

            var codigo = SomenteDigitos(cols[0]);
            if (codigo.Length < 4)
                continue;

            var tipo = ParseInt(Valor(cols, 2));
            if (tipo is not (null or 0))
                continue;

            lista.Add(new IbptAliquota
            {
                Codigo = codigo.Length >= 8 ? codigo[..8] : codigo.PadRight(8, '0'),
                Uf = (ufPadrao ?? "").Trim().ToUpperInvariant(),
                Ex = ParseInt(Valor(cols, 1)) ?? 0,
                Descricao = Valor(cols, 3),
                Nacional = ParseDecimal(Valor(cols, 4)),
                Importado = ParseDecimal(Valor(cols, 5)),
                Estadual = ParseDecimal(Valor(cols, 6)),
                Municipal = ParseDecimal(Valor(cols, 7)),
                VigenciaInicio = ParseData(Valor(cols, 8)) ?? vigIni,
                VigenciaFim = ParseData(Valor(cols, 9)) ?? vigFim,
                Chave = Valor(cols, 10) ?? chave,
                Versao = Valor(cols, 11) ?? versao,
                Fonte = Valor(cols, 12) ?? fonte,
                Origem = "tabela"
            });
        }

        return lista;
    }

    private static string[] Split(string linha) =>
        linha.Split(';', StringSplitOptions.None);

    private static bool EhCabecalho(string primeiro) =>
        primeiro.Contains("codigo", StringComparison.OrdinalIgnoreCase)
        || primeiro.Contains("código", StringComparison.OrdinalIgnoreCase);

    private static bool EhMetadado(string primeiro, string[] cols) =>
        primeiro.StartsWith("versao", StringComparison.OrdinalIgnoreCase)
        || primeiro.StartsWith("chave", StringComparison.OrdinalIgnoreCase)
        || primeiro.StartsWith("fonte", StringComparison.OrdinalIgnoreCase)
        || primeiro.StartsWith("vigencia", StringComparison.OrdinalIgnoreCase)
        || (cols.Length < 4 && !char.IsDigit(primeiro.TrimStart().FirstOrDefault()));

    private static void CapturarMetadado(
        string[] cols,
        ref string? versao,
        ref string? chave,
        ref string? fonte,
        ref DateTime? vigIni,
        ref DateTime? vigFim)
    {
        var k = cols[0].Trim().ToLowerInvariant();
        var v = cols.Length > 1 ? cols[1].Trim() : "";
        if (k.StartsWith("versao")) versao = v;
        else if (k.StartsWith("chave")) chave = v;
        else if (k.StartsWith("fonte")) fonte = v;
        else if (k.Contains("inicio")) vigIni = ParseData(v);
        else if (k.Contains("fim")) vigFim = ParseData(v);
    }

    private static string? Valor(string[] cols, int i) =>
        i < cols.Length ? cols[i].Trim().Trim('"') : null;

    private static string SomenteDigitos(string? s) =>
        new((s ?? "").Where(char.IsDigit).ToArray());

    private static int? ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static decimal ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0;
        if (decimal.TryParse(s, NumberStyles.Number, PtBr, out var br))
            return br;
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var inv))
            return inv;
        return 0;
    }

    private static DateTime? ParseData(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        var formatos = new[] { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
        if (DateTime.TryParseExact(s.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return DateTime.TryParse(s, PtBr, DateTimeStyles.None, out d) ? d : null;
    }
}
