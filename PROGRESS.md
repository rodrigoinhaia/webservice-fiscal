# PROGRESS — FiscalService

## Status Geral

🟢 **Fase 1, 2 e parte da Fase 3 entregues** — API estável, testes verdes,
endpoints de emissão / consulta / histórico operacionais.

---

## Fase Atual

**Fase 3 em andamento.** Itens entregues nesta sessão:

- ✅ Status SEFAZ multimodelo (NF-e / NFC-e / CT-e / MDF-e) com roteamento real.
- ✅ Sincronização emissão → numeração (`ConfirmarNumeroAsync` automático em todas as emissões).
- ✅ Endpoint `GET /api/emissoes` paginado + `GET /api/emissoes/{chave}`.
- ✅ Suite de testes unitários expandida (validators + helpers + mappers).
- ✅ Limpeza do `appsettings.json` (sem placeholders ofensivos / sem credenciais inertes).

Pendente para fechar Fase 3: DANFE multiplataforma, contingência SVC, retries, webhooks/async.

---

## Métricas

| Métrica | Status | Detalhe |
|---------|--------|---------|
| Build local | 🟢 | `dotnet build` Release sem warnings |
| Testes unitários | 🟢 | **90 testes** verdes (xUnit) — `tests/FiscalService.Api.Tests` |
| Testes de integração | 🟡 | 5 testes (Testcontainers) — passam no CI; locais saem como `SKIP` quando não há Docker |
| CI (GitHub Actions) | 🟢 | `ci.yml` faz `restore → build → test → docker build` |
| Docker build | 🟡 | Imagem multi-stage validada por CI; ainda sem deploy real homologação |
| Homologação NF-e | 🔴 | Pendente certificado A1 de teste SEFAZ |
| DANFE no Linux | 🔴 | `NotSupportedException` consciente — ver `docs/DANFE-ESTRATEGIA.md` |

---

## Tarefas Concluídas

### Setup e Infraestrutura
- [x] Solução `.sln` e projeto `FiscalService.Api.csproj` criados
- [x] NuGets: EF Core 8, Npgsql, DFe.NET, NFe.Danfe.Nativo, Serilog, HealthChecks
- [x] `appsettings.json` saneado (sem credenciais hard-coded)
- [x] `FiscalConfig` POCO com bind de configuração
- [x] `EnvBootstrap` — `.env`, aliases, montagem de connection string
- [x] `ApiKeyMiddleware` + `ApiKeyRing` (rotação por múltiplas chaves)
- [x] `Program.cs` — Serilog, DI, migrations automáticas, health check, OTel opcional, rate limit

### Banco de Dados
- [x] `AppDbContext` com `EmissaoLog` e `NumeracaoSequencial`
- [x] Índices únicos e compostos configurados via Fluent API
- [x] Migration `20260424145436_InitialCreate`

### Serviços Fiscais
- [x] `CertificadoService` — validar e fazer upload de `.pfx`
- [x] `NumeracaoService` — lock pessimista (`SELECT FOR UPDATE`) PostgreSQL
- [x] `NFeService` — emitir, cancelar, CC-e, consultar, inutilizar, status-sefaz
- [x] `NFCeService` — emitir (CSC/IdCSC), cancelar, status-sefaz
- [x] `CTeService` — emitir, cancelar, status-sefaz
- [x] `MDFeService` — emitir, encerrar, cancelar, status-sefaz
- [x] `DanfeService` — PDF base64 NF-e/NFC-e (lança `NotSupportedException` em Linux por design)
- [x] `EmissaoLogService` — listagem paginada + busca por chave
- [x] `UfHelper` — mapeamento dos 27 estados para `Estado` (IBGE)
- [x] **Sincronização emissão → numeração** em NF-e/NFC-e/CT-e/MDF-e

### Controllers (9 controllers)
- [x] `NFeController` — 6 endpoints
- [x] `NFCeController` — 2 endpoints
- [x] `CTeController` — 2 endpoints
- [x] `MDFeController` — 3 endpoints
- [x] `ConsultaController` — `status-servico` roteado por modelo
- [x] `DanfeController` — 2 endpoints
- [x] `CertificadoController` — 2 endpoints
- [x] `NumeracaoController` — 2 endpoints
- [x] `EmissoesController` — listagem paginada + busca por chave

### Telemetria & Observabilidade
- [x] Serilog com sink de arquivo opcional + degradação graciosa
- [x] OpenTelemetry OTLP opcional (traces + metrics)
- [x] Métrica custom `fiscal.sefaz.outcomes`
- [x] `FiscalResponseTelemetryFilter` global (todo `FiscalResponse` gera ponto)

### DevOps
- [x] `Dockerfile` multi-stage Linux (com fontes p/ DANFE futuro)
- [x] `docker-compose.yml` com PostgreSQL 16
- [x] `.env.example` com todas as variáveis
- [x] `.gitignore`
- [x] CI GitHub Actions (`build → test → docker build`)

### Testes
- [x] **90 testes unitários** (`xUnit` + `FluentValidation.TestHelper`):
  - `ApiKeyRingTests`
  - `ConfiguracaoEmitenteRequestValidatorTests`
  - `NFeEmitirRequestValidatorTests`
  - `NFCeEmitirRequestValidatorTests`
  - `CTeEmitirRequestValidatorTests`
  - `MDFeEmitirRequestValidatorTests`
  - `ItemNFeRequestValidatorTests`
  - `UfHelperTests`
  - `ImpostoIcmsMapperTests`
- [x] **5 testes de integração** (Testcontainers + Postgres real):
  - `NumeracaoServiceIntegrationTests` (3)
  - `EmissaoLogServiceIntegrationTests` (2)

### Documentação
- [x] `README.md` (uso, deploy, endpoints, exemplos)
- [x] `PLANNING.md` (objetivo, RF/RNF, arquitetura, stack, fases, riscos)
- [x] `docs/SMOKE-HOMOLOGACAO.md` (checklist pós-deploy)
- [x] `docs/DANFE-ESTRATEGIA.md` (estratégia multiplataforma)
- [x] `docs/CAPACIDADES.md` (catálogo detalhado — atualizado com status multimodelo, histórico de emissões e nova suite de testes)

---

## Tarefas Pendentes

### Operacional / Homologação
- [ ] Obter certificado A1 SEFAZ de homologação
- [ ] Executar checklist `docs/SMOKE-HOMOLOGACAO.md` em ambiente real
- [ ] Validar build/run da imagem Docker em servidor Linux

### Funcional / Roadmap (Fase 3 restante)
- [ ] DANFE multiplataforma (DanfeSharp ou QuestPDF, ou microsserviço externo)
- [ ] Suporte a certificado **A3 / HSM**
- [ ] Modais MDF-e não-rodoviários (`02`–`04`)
- [ ] Cenários ICMS-ST/Reduzido em CT-e
- [ ] Contingência SVC-AN / SVC-RS
- [ ] Distribuição DF-e / Manifestação do Destinatário
- [ ] Endpoints assíncronos com callback webhook
- [ ] Retry automático em falha de conectividade SEFAZ
- [ ] Cobertura de testes ≥ 80% (com métricas reportadas no CI)

---

## Próximo Passo Imediato

1. Provisionar certificado A1 de homologação e variáveis em ambiente real (Easypanel / VM Linux).
2. Subir a imagem Docker (`docker compose up -d --build`).
3. Rodar o checklist `docs/SMOKE-HOMOLOGACAO.md`:
   - `GET /health` → 200
   - `GET /api/emissoes?pagina=1&tamanhoPagina=10` (auth) → 200 + listagem vazia inicial
   - `POST /api/consulta/status-servico { modelo: "NFe" | "NFCe" | "CTe" | "MDFe" }` → cStat 107
   - Emitir NF-e de teste → conferir registro em `/api/emissoes` e contador em `/api/numeracao`.

---

## Indicadores

| Indicador | Valor Atual | Meta |
|-----------|-------------|------|
| Endpoints implementados | 22/22 | 22 |
| Serviços implementados | 8/8 | 8 |
| Testes unitários | 90 | ≥ 80% cobertura |
| Testes de integração | 5 | ≥ 5 (cobrindo numeração + logs) |
| Build passando | ✅ | ✅ |
| SEFAZ homologação | 🔴 Pendente certificado | ✅ |
