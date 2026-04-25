using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class DanfeNFeRequest
{
    /// <summary>XML do nfeProc (NF-e autorizada) em string.</summary>
    [Required]
    public string XmlNfeProc { get; set; } = string.Empty;
}

public class DanfeNFCeRequest
{
    /// <summary>XML do nfeProc da NFC-e em string.</summary>
    [Required]
    public string XmlNfeProc { get; set; } = string.Empty;

    [Required]
    public string IdCsc { get; set; } = string.Empty;

    [Required]
    public string Csc { get; set; } = string.Empty;
}
