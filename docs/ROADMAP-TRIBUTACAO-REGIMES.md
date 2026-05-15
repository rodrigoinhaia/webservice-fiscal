# Roadmap — Tributação completa (Simples, Lucro Presumido, Lucro Real)

Plano de tarefas para o `FiscalService.Api` atender **qualquer emissão NF-e/NFC-e** nos três enquadramentos usuais, com implementação e documentação alinhadas ao **[ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET)** e às Notas Técnicas da NF-e 4.0.

> **Nota sobre regimes:** na NF-e, **Lucro Presumido** e **Lucro Real** aparecem como **`crt: 3`** (regime normal). A diferença está no **cálculo no ERP** (CST, bases, alíquotas por item). **Simples Nacional** usa **`crt: 1` ou `2`** e **CSOSN** por item. O webservice deve montar o XML correto para o que o cliente enviar — não substitui o motor de cálculo do ERP.

**Referências oficiais do ecossistema**

| Recurso | Uso no projeto |
|--------|----------------|
| [DFe.NET](https://github.com/ZeusAutomacao/DFe.NET) | Classes `NFe.Classes.*`, `ServicosNFe`, schemas em `NFe.AppTeste/Schemas` |
| [NFe.AppTeste](https://github.com/ZeusAutomacao/DFe.NET/tree/master/NFe.AppTeste) | Exemplos de montagem de XML e chamadas SEFAZ |
| [Portal NF-e](http://www.nfe.fazenda.gov.br/portal/principal.aspx) | MOC, NTs, tabelas CST/CSOSN |
| `docs/CAPACIDADES.md` | Estado atual do microsserviço |
| `README.md` | Exemplos operacionais |

**Estado atual (baseline)** — ver `ImpostoIcmsMapper.cs`, `ImpostoItemFactory.cs`:

- CRT 3: CST `00`, `40`/`41`/`50`, `60` (demais → fallback `ICMS00`)
- CRT 1/2: CSOSN `101`–`103`, `201`–`203`, `500`, `900` (demais → `102`)
- PIS/COFINS: só `PISAliq` / `COFINSAliq` (default CST `07`)
- IPI: campos no DTO, **sem** grupo `<IPI>` no item
- Sem ISSQN, II, DIFAL dedicado, contingência SVC, cadastro de emitente

---

## Legenda de status

- [ ] Pendente
- [~] Em andamento
- [x] Concluído

---

## Fase 0 — Alinhamento com DFe.NET e governança

Objetivo: toda evolução tributária espelha tipos e fluxos da biblioteca, não reinventa XML.

| # | Tarefa | Entregável | Referência DFe.NET |
|---|--------|------------|-------------------|
| 0.1 | [ ] Documentar versão fixa ou faixa de `Zeus.Net.NFe.NFCe` no `FiscalService.Api.csproj` (evitar `*` em produção) | Seção em `README.md` + `Directory.Packages.props` ou pin de versão | Releases / tags do repositório |
| 0.2 | [ ] Script ou doc de sincronização de **schemas XSD** (`NFe.AppTeste/Schemas` → `src/FiscalService.Api/Schemas`) | `docs/SCHEMAS-DFE.md` + comando no README | Mesmo path usado em `NFe.AppTeste` |
| 0.3 | [x] Matriz **CST/CSOSN × classe C#** (`ICMS00`, `ICMSSN102`, …) vs suporte no API | Tabela em `docs/TRIBUTACAO-MATRIZ.md` | `NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual` |
| 0.4 | [ ] Checklist de conformidade por NT (link MOC + issue DFe.NET se layout divergir) | Item em `PLANNING.md` riscos | Issues/PRs ZeusAutomacao |
| 0.5 | [ ] Testes de regressão com **XMLs de homologação** gravados (golden files), inspirados em cenários do `NFe.AppTeste` | `tests/.../Fixtures/nfe/*.xml` | AppTeste / manuais SEFAZ |

---

## Fase 1 — ICMS regime normal (CRT 3) — Lucro Presumido e Lucro Real

Objetivo: cobrir CSTs usados em operações comuns de LP/LR sem fallback silencioso para `ICMS00`.

| # | Tarefa | Entregável | Classes DFe.NET (referência) |
|---|--------|------------|------------------------------|
| 1.1 | [x] **ICMS10** — tributada com ST | Mapper + DTO opcionais ST | `ICMS10` |
| 1.2 | [x] **ICMS20** — redução de base | `pRedBC`, `vBC`, `pICMS`, `vICMS` | `ICMS20` |
| 1.3 | [x] **ICMS30** — isenta/não tributada com ST | Campos ST + desoneração | `ICMS30` |
| 1.4 | [x] **ICMS51** — diferimento | `valorIcmsOperacao`, `percentualDiferimentoIcms`, `valorIcmsDiferido` | `ICMS51` |
| 1.5 | [x] **ICMS70** — redução + ST | Combinar redução e ST | `ICMS70` |
| 1.6 | [x] **ICMS90** — outros | Grupo flexível conforme payload | `ICMS90` |
| 1.7 | [~] **ICMSPart** / partilha (DIFAL) quando `idDest` interestadual consumidor final | `ICMSUFDest` por item + totais básicos | NT 2015/003 — classes UFDest |
| 1.8 | [ ] **FCP** (`ICMS00` + campos FCP, ST com FCP) | Campos `vFCP`, `vFCPST` nos totais | Grupos FCP no layout 4.0 |
| 1.9 | [x] Rejeitar CST não implementado com **400** explícito (não fallback para `00`) | FluentValidation + `TributacaoNaoSuportadaException` | — |
| 1.10 | [x] Testes unitários por CST (espelhar `ImpostoIcmsMapperTests`) | +1 teste por CST novo | — |
| 1.11 | [x] Exemplo JSON **LP** e **LR** em `docs/exemplos/nfe/crt3-*.json` | Payloads em `docs/exemplos/nfe/` | AppTeste regime normal |

---

## Fase 2 — Simples Nacional (CRT 1 e 2)

Objetivo: CSOSN e grupos SN completos para ME/EPP e excesso de sublimite.

| # | Tarefa | Entregável | Referência |
|---|--------|------------|------------|
| 2.1 | [ ] Validar **CSOSN 400** (se aplicável ao layout vigente) ou documentar ausência | Matriz atualizada | Tabela CSOSN Anexo I LC 123 |
| 2.2 | [ ] Revisar **103** vs **201** (grupo `ICMSSN201` no Zeus) — testes de schema | Testes + comentário no mapper | `ImpostoIcmsMapper` atual |
| 2.3 | [ ] NFC-e: regras SN + CSC alinhadas ao `NFCeService` e AppTeste NFCe | Doc `docs/exemplos/nfce/` | DFe.NET NFCe |
| 2.4 | [ ] Exemplo JSON **Simples CRT 1** (CSOSN 102) e **CRT 2** (excesso) | `docs/exemplos/nfe/crt1-sn102.json`, `crt2-*.json` | — |
| 2.5 | [ ] Documentar que **MEI** usa CRT 1 com restrições de CFOP/NCM (validação opcional no API) | Seção README tributação | Receita / NT |

---

## Fase 3 — PIS, COFINS, IPI e totais

Objetivo: federais compatíveis com indústria, atacado e monofásico.

| # | Tarefa | Entregável | Classes DFe.NET |
|---|--------|------------|-----------------|
| 3.1 | [x] **IPI** no item (`IPITrib`, `IPINT`) conforme `cstIpi` | `ImpostoItemFactory` + testes | `IPI`, `IPITrib`, `IPINT` |
| 3.2 | [x] **PISNT** / **COFINSNT** (CST 04–09) | `ImpostoItemFactory` | `PISNT`, `COFINSNT` |
| 3.3 | [ ] **PISQtde** / **COFINSQtde** | Campos qBCProd, vAliqProd | `PISQtde`, `COFINSQtde` |
| 3.4 | [x] **PISOutr** / **COFINSOutr** (CST 49, 99) | `ImpostoItemFactory` | `PISOutr`, `COFINSOutr` |
| 3.5 | [ ] Recalcular ou validar **ICMSTot** vs soma dos itens (vProd, vDesc, vST, vIPI, vPIS, vCOFINS) | Validator de consistência | `total.ICMSTot` |
| 3.6 | [ ] **II** (importação) — grupo quando CFOP de importação | `II` no item + `vII` no total | `II` |
| 3.7 | [ ] Exemplos `docs/exemplos/nfe/item-com-ipi.json`, `pis-cofins-nt.json` | JSON + nota no README | — |

---

## Fase 4 — ISSQN, serviços e operações especiais

Objetivo: notas mistas mercadoria + serviço e cenários LP/LR avançados.

| # | Tarefa | Entregável |
|---|--------|------------|
| 4.1 | [ ] Grupo **ISSQN** por item (`imposto.ISSQN`) + `total.ISSQNtot` | DTO + factory |
| 4.2 | [ ] **Exportação** — `idDest=3`, CFOP 7xxx, tags `exporta`, DI quando necessário | DTO + `NFeService` |
| 4.3 | [ ] **SUFRAMA** / ZFM (quando exigido pela UF) | Campos `ISUF` / regras por UF |
| 4.4 | [ ] **Combustível**, **medicamento**, **armas** (se escopo do produto) | Grupos específicos do layout — só se negócio exigir |
| 4.5 | [ ] **Carta de correção** e eventos: revisar sequência e campos vs `ServicosNFe` | Testes integrados mock |

---

## Fase 5 — CT-e, MDF-e e coerência multdocumento

| # | Tarefa | Entregável |
|---|--------|------------|
| 5.1 | [ ] CT-e: ICMS configurável (não só 12% fixo) — CST/modalidade | `CTeService` + testes |
| 5.2 | [ ] MDF-e: modais `02`–`04` além de rodoviário | `MDFeService` |
| 5.3 | [ ] Alinhar tributação CT-e/MDF-e à matriz da Fase 1 onde aplicável | `docs/TRIBUTACAO-MATRIZ.md` |

---

## Fase 6 — Plataforma, emitente e segurança

Objetivo: operação em produção sem reenviar certificado/senha em toda nota.

| # | Tarefa | Entregável |
|---|--------|------------|
| 6.1 | [x] Entidade **`Emitente`** (CNPJ, razão, IE, CRT default, UF, paths) + migration | `Data/Entities/Emitente.cs` + `AddEmitentes` |
| 6.2 | [x] Referência certificado por emitente (path + senha criptografada) | `CertificadoSenhaProtector` |
| 6.3 | [x] `POST/GET/PUT/DELETE /api/emitentes` | `EmitentesController` |
| 6.4 | [x] Emissão por **`emitenteCnpj`** com payload reduzido (NF-e/NFC-e + eventos NF-e) | `IEmitenteConfigSource` |
| 6.5 | [x] Validar **CNPJ do certificado = CNPJ do emitente** no cadastro/atualização | `validarCnpjCertificado` |
| 6.6 | [x] Health check: alerta certificado a expirar (&lt; 30 dias) | `CertificadosEmitentesHealthCheck` + `Fiscal:DiasAlertaCertificado` |

---

## Fase 7 — SEFAZ, contingência e pós-emissão

| # | Tarefa | Entregável | DFe.NET |
|---|--------|------------|---------|
| 7.1 | [x] **Contingência** SVC-AN / SVC-RS (`tpEmis`, dhCont, xJust) | `tipoEmissao` em `NFeEmitirRequest` | `ContingenciaEmissaoMapper` |
| 7.2 | [x] **Distribuição DF-e** + manifestação destinatário | `NFeDfeService` + rotas `/api/nfe/distribuicao-dfe`, `/manifestar-destinatario` | `NfeDistDFeInteresse` |
| 7.3 | [x] **Retry** político em timeout SEFAZ (falhas transitórias) | `SefazRetry` + `Fiscal:SefazRetry*` | manual |
| 7.4 | [ ] Emissão **assíncrona** + webhook (opcional) | Fila + callback | — |
| 7.5 | [~] Homologação formal: checklist `SMOKE-HOMOLOGACAO.md` por regime (SN, LP, LR) | Checklist expandido; evidências em `PROGRESS.md` pendentes de execução real | — |

---

## Fase 8 — Documentação e Swagger (exemplos alinhados)

| # | Tarefa | Entregável |
|---|--------|------------|
| 8.1 | [~] Pasta **`docs/exemplos/`** com JSON por cenário (emitir, cancelar, CC-e, NFC-e) | NF-e por regime entregue; cancelar/NFC-e pendente |
| 8.2 | [~] **`Swashbuckle`**: exemplos JSON de `docs/exemplos/` | `OpenApiJsonExamplesFilter` (NF-e emitir, emitentes, NFC-e) |
| 8.3 | [ ] Habilitar `GenerateDocumentationFile` + `IncludeXmlComments` nos DTOs | Summaries no Swagger UI |
| 8.4 | [ ] Página **`docs/GUIA-REGIMES.md`**: CRT 1/2/3, quem calcula o quê (ERP vs API), tabela CST/CSOSN suportados | Link no README |
| 8.5 | [x] Atualizar **`CAPACIDADES.md`** §6 e §20 a cada fase concluída | Emitentes, DF-e, retry, tributação, health |
| 8.6 | [ ] Coleção **Insomnia/Postman** exportada (`docs/postman/FiscalService.json`) | Importável |
| 8.7 | [ ] Referência cruzada: “equivalente AppTeste” por endpoint (link path GitHub DFe.NET) | Tabela em `GUIA-REGIMES.md` |

**Estrutura sugerida de exemplos**

```text
docs/exemplos/
├── README.md
├── nfe/
│   ├── crt1-simples-csosn102-homologacao.json
│   ├── crt2-excesso-sublimite.json
│   ├── crt3-lucro-presumido-icms00.json
│   ├── crt3-lucro-real-icms10-st.json
│   ├── crt3-icms40-isenta.json
│   ├── crt3-icms60-st-retido.json
│   └── crt3-interestadual-difal.json
├── nfce/
│   └── emitir-homologacao-csc.json
├── cte/
│   └── emitir-rodoviario.json
└── respostas/
    ├── fiscal-response-autorizado.json
    └── fiscal-response-rejeicao-sefaz.json
```

---

## Fase 9 — Qualidade e CI

| # | Tarefa | Meta |
|---|--------|------|
| 9.1 | [ ] Cobertura ≥ **80%** em `Services/Fiscal/*` e validators | Report no CI |
| 9.2 | [ ] Testes de integração: emitente + numeração + log (Testcontainers) | +3 cenários |
| 9.3 | [ ] Job opcional: validar XML gerado contra XSD local (`Schemas/`) | Falha no CI se schema inválido |
| 9.4 | [ ] Testes E2E homologação (marcados `[Explicit]`, não no CI padrão) | Documentar credenciais |

---

## Ordem de execução recomendada

```text
Fase 0 → Fase 1 → Fase 3.1 (IPI) → Fase 3.2–3.4 (PIS/COFINS)
       → Fase 8 (exemplos em paralelo)
       → Fase 6 (emitente)
       → Fase 7.1 (contingência)
       → Fase 2 (refino SN) → Fase 4–5 (escopo negócio)
       → Fase 7.2+ (DF-e/async)
```

**MVP “qualquer regime básico”:** concluir **Fase 0 + 1 + 3.1 + 8 + 7.5** (homologação SN + LP/LR com CSTs 00/10/20/40/60/90 e IPI).

---

## Critérios de “pronto para qualquer envio”

- [ ] Todos os CST CRT 3 da matriz (Fase 1) implementados ou rejeitados explicitamente
- [ ] Todos os CSOSN SN usados em produção (Fase 2) com testes
- [ ] PIS/COFINS/IPI conforme matriz (Fase 3)
- [ ] Exemplos JSON versionados e espelhados no Swagger (Fase 8)
- [ ] Homologação SEFAZ: pelo menos 1 nota autorizada por CRT (1, 2, 3) com evidência
- [ ] Sem fallback silencioso de CST/CSOSN para grupo genérico errado

---

## Atualização deste documento

Ao concluir uma tarefa: marcar `[x]`, registrar data em `PROGRESS.md` e atualizar `docs/CAPACIDADES.md` + matriz CST se aplicável.

*Criado em: 2026-05-15 — alinhado a FiscalService v1.0.0 e DFe.NET (ZeusAutomacao).*
