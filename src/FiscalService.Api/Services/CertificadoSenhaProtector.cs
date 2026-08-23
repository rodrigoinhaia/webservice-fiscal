using Microsoft.AspNetCore.DataProtection;

namespace FiscalService.Api.Services;

/// <summary>Protege senha do certificado A1 e CSC da NFC-e em repouso (IDataProtection).</summary>
public sealed class CertificadoSenhaProtector
{
    private readonly IDataProtector _senhaProtector;
    private readonly IDataProtector _cscProtector;

    public CertificadoSenhaProtector(IDataProtectionProvider provider)
    {
        _senhaProtector = provider.CreateProtector("FiscalService.Emitente.CertificadoSenha.v1");
        _cscProtector = provider.CreateProtector("FiscalService.Emitente.Csc.v1");
    }

    public string Proteger(string senhaEmTexto) => _senhaProtector.Protect(senhaEmTexto);

    public string Desproteger(string senhaProtegida) => _senhaProtector.Unprotect(senhaProtegida);

    public string ProtegerCsc(string cscEmTexto) => _cscProtector.Protect(cscEmTexto);

    public string DesprotegerCsc(string cscProtegido) => _cscProtector.Unprotect(cscProtegido);
}
