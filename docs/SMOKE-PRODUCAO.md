# Smoke test — produção

Checklist para validar o FiscalService em **produção** após deploy ou alteração crítica.

> **Atenção:** este fluxo gera documentos fiscais **reais** na SEFAZ / NFS-e Nacional e os cancela em seguida. Use apenas com emitente e certificado de produção já cadastrados.

## Pré-condições

| Item | Verificação |
|------|-------------|
| Ambiente | `FISCAL__Ambiente=Producao` no servidor (ou emitente com `ambiente: Producao`) |
| Emitente | `POST /api/emitentes` já realizado com certificado A1 de produção |
| Data Protection | Volume persistente em `/app/keys` (senhas de certificado/CSC) |
| Numeração | Contadores conferidos em `/api/numeracao` (último + próximo por série) |
| NFC-e | CSC de produção cadastrado no emitente (`idCscProducao` / `cscProducao`) ou em `producao.env` |

## Cliente de teste (destinatário / tomador)

Dados do CNPJ **SDBR SOLUCOES DIGITAIS LTDA** (comprovante RFB):

| Campo | Valor |
|-------|--------|
| CNPJ | `53658565000127` |
| Razão social | SDBR SOLUCOES DIGITAIS LTDA |
| Endereço | AV MARQ DE SAO VICENTE, 2219, CONJ 812 — Água Branca |
| Município/UF | São Paulo / SP (IBGE `3550308`) |
| CEP | `05036040` |
| E-mail | `contato@sdbr.app` |

Payloads em [`docs/exemplos/producao/`](exemplos/producao/).

## Configuração

```powershell
copy scripts\config\producao.env.example scripts\config\producao.env
# Edite: SMOKE_BASE_URL, SMOKE_API_KEY, SMOKE_CNPJ
```

O arquivo `producao.env` **não é versionado** (`.gitignore`).

## Execução

### 1. Pré-checks (sem emissão)

Valida health, autenticação, emitente e numeração:

```powershell
.\scripts\smoke-producao.ps1
```

### 2. Emitir e cancelar (produção)

**NFS-e** (padrão — tomador SDBR, valor R$ 1,00):

```powershell
.\scripts\smoke-producao.ps1 -ConfirmarProducao
```

**NF-e** interestadual RS → SP:

```powershell
.\scripts\smoke-producao.ps1 -Modelo NFe -ConfirmarProducao
```

**NFC-e** (CSC no emitente ou parâmetros):

```powershell
.\scripts\smoke-producao.ps1 -Modelo NFCe -ConfirmarProducao -IdCsc "1" -Csc "SEU_CSC"
```

**Todos os modelos:**

```powershell
.\scripts\smoke-producao.ps1 -Modelo Todos -ConfirmarProducao
```

### Dry-run

```powershell
.\scripts\smoke-producao.ps1 -ConfirmarProducao -DryRun
```

## Fluxo por modelo

```text
GET /health
GET /api/emitentes/{cnpj}
GET /api/numeracao?cnpj=&ambiente=Producao
POST /api/{nfe|nfce|nfse}/emitir     → cStat 100
POST /api/{nfe|nfse}/consultar       → (opcional)
POST /api/{nfe|nfce|nfse}/cancelar   → cStat 100
```

Evidências em `scripts/smoke-output/producao-*.jsonl` (não versionado).

## Valores de teste

| Modelo | Valor | Série padrão |
|--------|-------|--------------|
| NF-e | R$ 1,00 | 3 |
| NFC-e | R$ 1,00 | 2 |
| NFS-e | R$ 1,00 | 1 |

Todos os payloads incluem observação de teste e são cancelados logo após autorização.

## Troubleshooting

| Erro | Ação |
|------|------|
| `CryptographicException` / key ring | Verificar volume `/app/keys`; re-salvar senha do certificado no emitente |
| `401` | Conferir `SMOKE_API_KEY` |
| Rejeição SEFAZ (numeração) | Conferir `GET /api/numeracao` e ajustar com `POST /api/numeracao/confirmar` |
| NFC-e sem CSC | Cadastrar `cscProducao` no emitente ou passar `-IdCsc` / `-Csc` |
| NFS-e rejeitada | Conferir `codTributacaoNacional`, IM do prestador e módulo `FISCAL_NFSe_Habilitado=true` |

## Homologação vs produção

Execute sempre [SMOKE-HOMOLOGACAO.md](SMOKE-HOMOLOGACAO.md) em homologação antes de usar este script em produção.
