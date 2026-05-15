using Microsoft.AspNetCore.DataProtection;

namespace FiscalService.Api.Services;

/// <summary>Protege a senha do certificado A1 em repouso (IDataProtection).</summary>
public sealed class CertificadoSenhaProtector
{
    private readonly IDataProtector _protector;

    public CertificadoSenhaProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("FiscalService.Emitente.CertificadoSenha.v1");
    }

    public string Proteger(string senhaEmTexto) => _protector.Protect(senhaEmTexto);

    public string Desproteger(string senhaProtegida) => _protector.Unprotect(senhaProtegida);
}
