# Scripts operacionais

## `smoke-homologacao.ps1`

Automatiza o checklist de homologação SEFAZ documentado em [`docs/SMOKE-HOMOLOGACAO.md`](../docs/SMOKE-HOMOLOGACAO.md).

**Pré-requisitos:** API rodando, `.env` na raiz com `API_KEY`, certificado `.pfx` em `certificados/`, exemplos em `docs/exemplos/` ajustados para seu CNPJ.

**Saída:** log JSONL em `smoke-output/` (gitignored).

```powershell
.\scripts\smoke-homologacao.ps1 -Cnpj "SEU_CNPJ_14_DIGITOS" -CertificadoSenha "..." -CadastrarEmitente -EmitirNFe
```

Use `-DryRun` para listar passos sem chamar a API.
