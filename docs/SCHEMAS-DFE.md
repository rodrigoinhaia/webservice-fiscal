# Sincronização de schemas XSD (DFe.NET)

Os XSD em `src/FiscalService.Api/Schemas` validam XMLs antes do envio à SEFAZ quando `ValidarSchemas` está ativo na `ConfiguracaoServico`.

## Origem oficial

Repositório [ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET), pasta:

`NFe.AppTeste/Schemas/`

## Atualizar (Linux / macOS / Git Bash)

```bash
git clone --depth 1 https://github.com/ZeusAutomacao/DFe.NET.git /tmp/dfe-net
cp -r /tmp/dfe-net/NFe.AppTeste/Schemas/* src/FiscalService.Api/Schemas/
```

## Atualizar (PowerShell)

```powershell
$tmp = Join-Path $env:TEMP "dfe-net-schemas"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
git clone --depth 1 https://github.com/ZeusAutomacao/DFe.NET.git $tmp
Copy-Item -Path (Join-Path $tmp "NFe.AppTeste\Schemas\*") -Destination "src\FiscalService.Api\Schemas\" -Recurse -Force
```

## Versão do pacote NuGet

Mantenha `Zeus.Net.NFe.NFCe`, `Zeus.Net.CTe` e `Zeus.Net.MDFe` na **mesma versão** fixada no `FiscalService.Api.csproj`. Após atualizar o pacote, sincronize os schemas e rode os testes.

## Docker

O `Dockerfile` copia `Schemas/` para `/app/schemas`. Rebuild da imagem após alterar XSDs.
