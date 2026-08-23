using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace FiscalService.Api.Services;

/// <summary>Protege senha do certificado A1 e CSC da NFC-e em repouso (IDataProtection).</summary>
public sealed class CertificadoSenhaProtector
{
    private const string MensagemKeyRing =
        "Não foi possível descriptografar a senha do certificado: o key ring do Data Protection mudou " +
        "(volume /app/keys ausente ou perdido no redeploy). Monte um volume persistente em /app/keys e " +
        "atualize o emitente com certificadoSenha novamente (PUT/PATCH /api/emitentes/{cnpj}).";

    private readonly IDataProtector _senhaProtector;
    private readonly IDataProtector _cscProtector;

    public CertificadoSenhaProtector(IDataProtectionProvider provider)
    {
        _senhaProtector = provider.CreateProtector("FiscalService.Emitente.CertificadoSenha.v1");
        _cscProtector = provider.CreateProtector("FiscalService.Emitente.Csc.v1");
    }

    public string Proteger(string senhaEmTexto) => _senhaProtector.Protect(senhaEmTexto);

    public string Desproteger(string senhaProtegida)
    {
        try
        {
            return _senhaProtector.Unprotect(senhaProtegida);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(MensagemKeyRing, ex);
        }
    }

    public string ProtegerCsc(string cscEmTexto) => _cscProtector.Protect(cscEmTexto);

    public string DesprotegerCsc(string cscProtegido)
    {
        try
        {
            return _cscProtector.Unprotect(cscProtegido);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(MensagemKeyRing, ex);
        }
    }
}
