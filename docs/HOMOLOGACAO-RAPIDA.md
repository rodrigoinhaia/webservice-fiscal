# Homologação rápida — 15 minutos

Guia mínimo para validar **uma NF-e autorizada** na SEFAZ de homologação com o seu CNPJ/UF.

Checklist completo: [`SMOKE-HOMOLOGACAO.md`](SMOKE-HOMOLOGACAO.md) · Script: [`../scripts/smoke-minimo.ps1`](../scripts/smoke-minimo.ps1)

---

## Pré-requisitos (5 min)

| # | Item |
|---|------|
| 1 | API no ar (`docker compose up -d` ou `dotnet run`) |
| 2 | `.env` na raiz com `API_KEY` e banco PostgreSQL |
| 3 | Certificado **A1 de homologação** (`.pfx`) — CNPJ do cert = CNPJ do emitente |
| 4 | `FISCAL_AMBIENTE=Homologacao` no `.env` ou no cadastro do emitente |

```powershell
# Na raiz do repositório
copy .env.example .env
# Edite API_KEY e DB_PASSWORD

copy scripts\config\homologacao.env.example scripts\config\homologacao.env
# Edite SMOKE_CNPJ, SMOKE_UF, SMOKE_CERTIFICADO_*
```

Copie o `.pfx` para `src\FiscalService.Api\certificados\` com o mesmo nome de `SMOKE_CERTIFICADO_PATH`.

---

## Passo a passo manual (opcional)

### 1. Health

```http
GET http://localhost:5555/health
```

Esperado: `"status": "healthy"`, `"banco": "healthy"`.

### 2. Cadastrar emitente

Ajuste [`exemplos/emitente/cadastro-homologacao.json`](exemplos/emitente/cadastro-homologacao.json) ou use o template.

```http
POST http://localhost:5555/api/emitentes
X-Api-Key: {API_KEY}
Content-Type: application/json
```

### 3. Emitir NF-e

Use `emitenteCnpj` (sem senha no body): [`exemplos/nfe/emitir-via-emitente-cnpj.json`](exemplos/nfe/emitir-via-emitente-cnpj.json).

```http
POST http://localhost:5555/api/nfe/emitir
```

Sucesso: `"sucesso": true`, `"codigoStatus": "100"`, `chaveAcesso` com 44 dígitos.

### 4. Registrar evidência

No `PROGRESS.md`, anote:

```text
Data: ____/____/____
CNPJ: ________________
UF: __
cStat emissão: 100
Chave: ____________________________________________
Protocolo: ______________
```

---

## Um comando (recomendado)

```powershell
cd C:\ProjetosLocais\webservice-fiscal

# Opção A — variáveis em scripts/config/homologacao.env
.\scripts\smoke-minimo.ps1

# Opção B — parâmetros na linha de comando
.\scripts\smoke-minimo.ps1 `
  -Cnpj "12345678000199" `
  -Uf "RS" `
  -CertificadoSenha "senha-do-pfx" `
  -CertificadoPath "empresa-homologacao.pfx"
```

O script executa: health → auth → cadastro emitente → numeração → 1 NF-e Simples → validação tributária (422 esperado).

Evidências: `scripts/smoke-output/evidencias-*.jsonl`

---

## CRT 3 (Lucro Presumido / Real)

Após o smoke mínimo OK, rode regime normal:

```powershell
.\scripts\smoke-homologacao.ps1 `
  -Cnpj "12345678000199" `
  -CertificadoSenha "senha-do-pfx" `
  -EmitirNFe `
  -EmitirTodosRegimes
```

Cadastre emitente com `"crt": 3` ou use os JSON em `exemplos/nfe/crt3-*.json`.

---

## Problemas comuns

| Sintoma | Causa provável |
|---------|----------------|
| 401 | `X-Api-Key` ausente ou diferente do `.env` |
| Certificado inválido | PFX/senha errados ou arquivo fora de `certificados/` |
| CNPJ certificado ≠ cadastro | Ative `validarCnpjCertificado` ou alinhe CNPJs |
| Rejeição SEFAZ (422) | Numeração/IE/CFOP/dados do emitente — leia `erro.detalhe` e `cStat` |
| `certificados_emitentes` unhealthy | PFX expirado ou path incorreto no cadastro |

---

## Próximo após homologação OK

1. Repetir para **CRT 2** e **CRT 3** se usar os três regimes.  
2. Testar **NFC-e** com CSC (`-EmitirNFCe` no smoke completo).  
3. Deploy em servidor Linux + backup de `xmls` e banco.  
4. Só então considerar **produção** (`ambiente: Producao` + certificado produção).
