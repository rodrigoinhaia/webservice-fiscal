# Scripts operacionais

## `smoke-minimo.ps1` (comece aqui)

Homologação em um comando após configurar `scripts/config/homologacao.env`:

```powershell
copy scripts\config\homologacao.env.example scripts\config\homologacao.env
# Edite CNPJ, senha do PFX, UF
.\scripts\smoke-minimo.ps1
```

Guia: [`docs/HOMOLOGACAO-RAPIDA.md`](../docs/HOMOLOGACAO-RAPIDA.md)

---

## `smoke-homologacao.ps1`

Automatiza o checklist de homologação SEFAZ documentado em [`docs/SMOKE-HOMOLOGACAO.md`](../docs/SMOKE-HOMOLOGACAO.md).

**Pré-requisitos:** API rodando, `.env` na raiz com `API_KEY`, certificado `.pfx` em `certificados/`, exemplos em `docs/exemplos/` ajustados para seu CNPJ.

**Saída:** log JSONL em `smoke-output/` (gitignored).

```powershell
.\scripts\smoke-homologacao.ps1 -Cnpj "SEU_CNPJ_14_DIGITOS" -CertificadoSenha "..." -CadastrarEmitente -EmitirNFe
```

Use `-DryRun` para listar passos sem chamar a API.
