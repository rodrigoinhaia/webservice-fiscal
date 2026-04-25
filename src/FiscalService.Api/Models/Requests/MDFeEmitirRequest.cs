using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class MDFeEmitirRequest
{
    [Required]
    public ConfiguracaoEmitenteRequest ConfiguracaoEmitente { get; set; } = null!;

    public int NumeroNota { get; set; }
    public string Serie { get; set; } = "1";

    /// <summary>Modal: 01=Rodoviário, 02=Aéreo, 03=Aquaviário, 04=Ferroviário.</summary>
    public string Modal { get; set; } = "01";

    public string UfInicio { get; set; } = string.Empty;
    public string UfFim { get; set; } = string.Empty;

    /// <summary>Data/hora de início do carregamento (UTC).</summary>
    public DateTime DataHoraInicio { get; set; } = DateTime.UtcNow;

    public List<MunicipioCarregamentoRequest> MunicipiosCarregamento { get; set; } = new();
    public List<PercursoMDFeRequest> Percurso { get; set; } = new();
    public List<DocumentoMDFeRequest> Documentos { get; set; } = new();

    public string? InformacoesAdicionais { get; set; }
}

public class MunicipioCarregamentoRequest
{
    public string CodigoMunicipio { get; set; } = string.Empty;
    public string NomeMunicipio { get; set; } = string.Empty;
}

public class PercursoMDFeRequest
{
    public string Uf { get; set; } = string.Empty;
}

public class DocumentoMDFeRequest
{
    /// <summary>Chave de acesso do CT-e ou NF-e vinculado ao MDF-e.</summary>
    public string ChaveAcesso { get; set; } = string.Empty;

    /// <summary>Segmento: "CTe" ou "NFe".</summary>
    public string TipoDocumento { get; set; } = "CTe";

    public string CodigoMunicipioDescarga { get; set; } = string.Empty;
    public string NomeMunicipioDescarga { get; set; } = string.Empty;
}

public class MDFeEncerrarRequest
{
    [Required]
    public ConfiguracaoEmitenteRequest ConfiguracaoEmitente { get; set; } = null!;

    [Required]
    [StringLength(44, MinimumLength = 44)]
    public string ChaveAcesso { get; set; } = string.Empty;

    [Required]
    public string Protocolo { get; set; } = string.Empty;

    public string CodigoMunicipioEncerramento { get; set; } = string.Empty;
    public string UfEncerramento { get; set; } = string.Empty;
    public DateTime DataEncerramento { get; set; } = DateTime.UtcNow;
}

public class MDFeCancelarRequest
{
    [Required]
    public ConfiguracaoEmitenteRequest ConfiguracaoEmitente { get; set; } = null!;

    [Required]
    [StringLength(44, MinimumLength = 44)]
    public string ChaveAcesso { get; set; } = string.Empty;

    [Required]
    public string Protocolo { get; set; } = string.Empty;

    [Required]
    [MinLength(15)]
    public string Justificativa { get; set; } = string.Empty;
}
