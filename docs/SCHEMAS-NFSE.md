# Schemas XSD — NFS-e Padrão Nacional

XSDs em `src/FiscalService.Api/Schemas/NFSe/` (pastas `1.00` e `1.01`), sincronizados com o pacote NuGet **`OpenAC.Net.NFSe.Nacional`**.

## Configuração

| Chave | Padrão | Descrição |
|-------|--------|-----------|
| `Fiscal:NFSe:DiretorioSchemas` | `/app/schemas/nfse` | Raiz dos XSD (subpastas por versão) |
| `Fiscal:NFSe:VersaoDps` | `Ve101` | Layout DPS (`Ve100` ou `Ve101`) |
| `Fiscal:NFSe:Habilitado` | `true` | Desliga endpoints `/api/nfse` quando `false` |

O serviço resolve automaticamente a subpasta `1.00` ou `1.01` conforme `VersaoDps`.

**Não misturar** com os XSD do DFe.NET (NF-e/NFC-e) em `Schemas/` na raiz — ver [`docs/SCHEMAS-DFE.md`](SCHEMAS-DFE.md).

## Sincronização

Copiar do pacote NuGet (após `dotnet restore`):

```powershell
$pkg = Join-Path $env:USERPROFILE ".nuget\packages\openac.net.nfse.nacional\<versao>\content\Schemas"
Copy-Item -Path "$pkg\*" -Destination "src\FiscalService.Api\Schemas\NFSe\" -Recurse -Force
```

Ou do repositório [OpenAC-Net/OpenAC.Net.NFSe.Nacional](https://github.com/OpenAC-Net/OpenAC.Net.NFSe.Nacional) (`Documentos/`).

## Docker

O `Dockerfile` copia `Schemas/` para `/app/schemas`; NFS-e fica em `/app/schemas/nfse/1.01` (ou `1.00`). Rebuild após atualizar XSDs.

## Referências

- [OpenAC.Net.NFSe.Nacional](https://github.com/OpenAC-Net/OpenAC.Net.NFSe.Nacional)
- Sistema Nacional NFS-e (Sefin / ADN)
