# Smoke test — homologação SEFAZ

Checklist manual após deploy ou alteração relevante no `FiscalService.Api`. Execute sempre em **homologação** antes de produção.

Registre evidências (data, operador, `cStat`, trecho de resposta) em `PROGRESS.md` ou anexo interno do time.

## Homologação rápida (primeira vez)

Guia enxuto com CNPJ/UF e um comando: **[`HOMOLOGACAO-RAPIDA.md`](HOMOLOGACAO-RAPIDA.md)** → `.\scripts\smoke-minimo.ps1`

---

## Script automatizado (PowerShell)

Na raiz do repositório, com a API no ar e `.env` preenchido (`API_KEY`, `DB_*`, `FISCAL_AMBIENTE=Homologacao`):

```powershell
# Mínimo: health + auth + cadastro emitente + 1 NF-e
.\scripts\smoke-homologacao.ps1 `
  -Cnpj "12345678000199" `
  -CertificadoSenha "senha-do-pfx" `
  -CadastrarEmitente `
  -EmitirNFe `
  -TestarTributacaoInvalida

# Completo: todos os regimes de exemplo + distribuição DF-e
.\scripts\smoke-homologacao.ps1 `
  -Cnpj "12345678000199" `
  -CertificadoSenha "senha-do-pfx" `
  -CadastrarEmitente `
  -EmitirNFe `
  -EmitirTodosRegimes `
  -TestarDistribuicaoDfe `
  -TestarTributacaoInvalida

# NFC-e (informe CSC de homologação da UF)
.\scripts\smoke-homologacao.ps1 -Cnpj "..." -CertificadoSenha "..." -CadastrarEmitente -EmitirNFCe -IdCsc "1" -Csc "SEU_CSC"
```

Evidências em `scripts/smoke-output/evidencias-*.jsonl` (não versionado). Saída com código `1` se algum passo falhar.

Parâmetros: `Get-Help .\scripts\smoke-homologacao.ps1 -Full`.

## Pré-condições

| Item | Verificação |
|------|-------------|
| Ambiente | `Fiscal__Ambiente=Homologacao` (global ou por emitente) |
| API | `ASPNETCORE_ENVIRONMENT=Development` se usar Swagger; porta exposta (ex. `5555`) |
| Segredos | `ApiKey`, `Database__ConnectionString` (ou `.env` com `DB_PASSWORD` / `DATABASE_URL`) |
| Banco | PostgreSQL acessível; migrations aplicadas |
| Certificado | A1 válido para **homologação**, CNPJ alinhado ao emitente de teste |
| Schemas | `Fiscal:DiretorioSchemas` apontando para `FiscalService.Api/Schemas` |
| CSC (NFC-e) | `idCsc` + `csc` de homologação da UF |

## 1. Saúde e infraestrutura

```http
GET /health
```

| Esperado | Critério |
|----------|----------|
| HTTP 200 | `status` = `healthy` ou `degraded` (degraded só se certificado próximo do vencimento) |
| `banco` | `healthy` |
| `checks.postgresql` | `healthy` |
| `checks.certificados_emitentes` | `healthy` (após cadastrar emitente com PFX válido) |
| `schemas` | `ok` |

Sem `X-Api-Key` nesta rota.

## 2. Autenticação

```http
GET /api/nfe/status-sefaz?uf=SP&ambiente=Homologacao&certificadoPath=...&certificadoSenha=...&cnpj=...
```

| Cenário | HTTP |
|---------|------|
| Sem `X-Api-Key` | 401 |
| Com chave correta | 200, `sucesso` conforme SEFAZ (`cStat` 107 = serviço em operação) |

## 3. Cadastro de emitente

1. Ajuste `docs/exemplos/emitente/cadastro-homologacao.json` (CNPJ, paths, CRT, UF).
2. Envie:

```http
POST /api/emitentes
X-Api-Key: {sua-chave}
Content-Type: application/json
```

3. Confirme `GET /api/emitentes/{cnpj}` — senha do PFX **não** deve aparecer em claro na resposta.
4. `/health` → `certificados_emitentes` deve permanecer `healthy`.

## 4. NF-e por regime (CRT)

Use os exemplos em `docs/exemplos/nfe/` substituindo `emitenteCnpj` pelo CNPJ cadastrado.

| Regime | CRT | Exemplo JSON | Objetivo |
|--------|-----|--------------|----------|
| Simples Nacional | 1 ou 2 | `crt1-simples-csosn102-homologacao.json` | CSOSN 102, emissão básica |
| Lucro Presumido | 3 | `crt3-lucro-presumido-icms00.json` | CST 00 |
| Lucro Real | 3 | `crt3-lucro-real-icms10-st.json` | CST 10 + ST |
| Interestadual | 3 | `crt3-interestadual-difal.json` | DIFAL (`ICMSUFDest`) |
| Com IPI | 3 | `crt3-item-com-ipi.json` | IPI tributado |
| ICMS redução | 3 | `crt3-icms20-reducao-base.json` | CST 20 |

```http
POST /api/nfe/emitir
X-Api-Key: {sua-chave}
```

Body mínimo com emitente cadastrado: `docs/exemplos/nfe/emitir-via-emitente-cnpj.json`.

| Resultado | Evidência |
|-----------|-----------|
| Sucesso | `sucesso: true`, `codigoStatus: "100"`, `chaveAcesso` 44 dígitos, `xmlAutorizado` preenchido |
| Rejeição esperada em teste inválido | `422`, `erro.tipo: RejeicaoSefaz`, `detalhe` com `cStat` |

Anote: chave, protocolo, número/série usados.

## 5. Eventos NF-e

Com uma NF-e autorizada em homologação:

| Passo | Rota | Body exemplo |
|-------|------|----------------|
| Consulta | `POST /api/nfe/consultar` | `chaveAcesso` + `emitenteCnpj` |
| CC-e | `POST /api/nfe/carta-correcao` | correção ≥15 caracteres |
| Cancelamento | `POST /api/nfe/cancelar` | protocolo + justificativa ≥15 chars |

Critério: eventos com `codigoStatus` de sucesso do evento (ex. `135` cancelamento).

## 6. Contingência (opcional)

`docs/exemplos/nfe/contingencia-svc-an.json` — `tipoEmissao: SVC-AN`, `justificativaContingencia` ≥15 caracteres.

Validar no XML: `tpEmis` diferente de normal e presença de `dhCont` / `xJust`.

## 7. Distribuição DF-e e manifestação

Pré-requisito: NF-e em que o CNPJ cadastrado seja **destinatário** (ou use `documentoInteressado` adequado).

```http
POST /api/nfe/distribuicao-dfe
```

Body: `docs/exemplos/nfe/distribuicao-dfe.json` (`ultNsu: "0"` na primeira consulta).

| cStat | Significado usual |
|-------|-------------------|
| 138 | Documentos localizados |
| 137 | Nenhum documento no NSU |
| 656 | Consumo indevido — aguardar antes de nova consulta |

Manifestação (após localizar chave):

```http
POST /api/nfe/manifestar-destinatario
```

Body: `docs/exemplos/nfe/manifestar-ciencia.json` — `tipoManifestacao: Ciencia`.

## 8. NFC-e

- Emitir com `idCsc`, `csc` e itens válidos para CRT do emitente.
- Verificar `infNFeSupl` com `qrCode` e `urlChave` no XML autorizado.
- Cancelar uma NFC-e de teste se a política da SEFAZ permitir.

## 9. CT-e / MDF-e (se no escopo do deploy)

- Uma emissão de teste + cancelamento/encerramento conforme manual da UF.

## 10. Regressão rápida

| Rota | Esperado |
|------|----------|
| `GET /api/numeracao/{cnpj}/55/{serie}` | Próximo número reservado |
| `POST /api/certificado/validar` | `valido: true` para PFX de teste |
| `GET /api/emissoes?cnpj=...` | Logs das emissões do smoke |
| Retry SEFAZ | Simular timeout (rede) — logs devem mostrar retentativa antes de falhar |

## 11. Tributação inválida (sanidade)

Enviar item com CST inexistente para CRT 3 (ex. `cstIcms: "99"` sem mapeamento):

- Esperado: **422**, `erro.tipo: TributacaoInvalida` (sem chamada SEFAZ).

## Critério de “passou”

- Nenhum **5xx** inesperado nos fluxos acima.
- Pelo menos **uma NF-e autorizada** (`cStat 100`) por CRT usado em produção (mínimo: CRT do cliente).
- Status SEFAZ, emitente cadastrado, `/health` e autenticação OK.
- Logs sem stack trace de configuração (connection string, certificado ausente, schemas).
- Evidências registradas para auditoria interna.

## Referências

- Capacidades: `docs/CAPACIDADES.md`
- Matriz tributária: `docs/TRIBUTACAO-MATRIZ.md`
- Roadmap: `docs/ROADMAP-TRIBUTACAO-REGIMES.md`
- Exemplos: `docs/exemplos/README.md`
