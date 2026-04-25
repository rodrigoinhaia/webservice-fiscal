namespace FiscalService.Api.Models.Responses;

/// <summary>Resposta padrão de emissão/evento fiscal.</summary>
public class FiscalResponse
{
    public bool Sucesso { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? Protocolo { get; set; }
    public string? CodigoStatus { get; set; }
    public string? Mensagem { get; set; }
    public string? XmlAutorizado { get; set; }
    public string? XmlEnviado { get; set; }
    public string? XmlRetorno { get; set; }
    public string? DanfePdfBase64 { get; set; }
    public ErroResponse? Erro { get; set; }

    public static FiscalResponse Ok(string chaveAcesso, string protocolo, string cStat, string mensagem, string? xml = null, string? pdf = null)
        => new()
        {
            Sucesso = true,
            ChaveAcesso = chaveAcesso,
            Protocolo = protocolo,
            CodigoStatus = cStat,
            Mensagem = mensagem,
            XmlAutorizado = xml,
            DanfePdfBase64 = pdf
        };

    public static FiscalResponse Falha(string tipo, string mensagem, string? detalhe = null, string? xmlEnviado = null, string? xmlRetorno = null)
        => new()
        {
            Sucesso = false,
            XmlEnviado = xmlEnviado,
            XmlRetorno = xmlRetorno,
            Erro = new ErroResponse
            {
                Tipo = tipo,
                Mensagem = mensagem,
                Detalhe = detalhe,
                Timestamp = DateTime.UtcNow
            }
        };
}

public class ErroResponse
{
    public string Tipo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public string? Detalhe { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
