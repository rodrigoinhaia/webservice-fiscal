# PROGRESS — FiscalService

## Status Geral

🟢 **MVP tributário NF-e/NFC-e + módulo NFS-e Nacional** — cadastro de emitentes, tributação ampliada, contingência, DF-e, retry SEFAZ, OpenAC ADN.

🔴 **Homologação SEFAZ/ADN real** — aguarda certificado A1 de teste e execução do smoke com evidências.

---

## Fase Atual

**Operacional + qualidade:** homologação end-to-end, cobertura de testes, DANFE PDF Linux.

---

## Métricas

| Métrica | Status | Detalhe |
|---------|--------|---------|
| Build local | 🟢 | `dotnet build` Release |
| Testes unitários | 🟢 | **158** testes (xUnit) |
| Testes de integração | 🟡 | Testcontainers — EF 8.0.27 alinhado |
| CI (GitHub Actions) | 🟢 | build + test + docker |
| Homologação NF-e | 🔴 | Checklist pronto; execução pendente |
| DANFE PDF Linux | 🔴 | HTML disponível |

---

## Entregas recentes (2026-08)

- [x] **IBPT / Lei 12.741/2012** — `vTotTrib` + `infCpl` na NF-e/NFC-e, API De Olho no Imposto + tabela local, token por emitente (UI no `webservice-fiscal-painel`)
- [x] **Módulo NFS-e Padrão Nacional** — `OpenAC.Net.NFSe.Nacional` 1.5.0, `/api/nfse`, schemas `Schemas/NFSe/`
- [x] Emitente: `inscricaoMunicipal`, `email`; migration chave 50 / série 5
- [x] Testes mapper/validator NFS-e; exemplos `docs/exemplos/nfse/`

## Entregas recentes (2026-05)

- [x] Cadastro de emitentes + `emitenteCnpj` + health de certificados
- [x] ICMS CRT 3 (CST 00–90), Simples (CSOSN), IPI, PIS/COFINS (incl. CST 03), DIFAL
- [x] `NFeTotaisCalculator` — FCP, ST, DIFAL nos totais + validação bruto × qtd
- [x] Contingência SVC-AN/RS/Offline, `SefazRetry`, distribuição DF-e, manifestação
- [x] Pin `Zeus.Net.*` `2026.8.18.2047`, `docs/SCHEMAS-DFE.md`, `docs/GUIA-REGIMES.md`
- [x] Exemplos: cancelar, CC-e, NFC-e, PIS NT; Swagger ampliado
- [x] `CAPACIDADES.md`, `README.md`, `SMOKE-HOMOLOGACAO.md` atualizados

---

## Tarefas Pendentes

### Bloqueador

- [ ] Certificado A1 homologação + smoke [`docs/SMOKE-HOMOLOGACAO.md`](docs/SMOKE-HOMOLOGACAO.md) com evidências (CRT 1, 2, 3)

### Produto

- [ ] Emissão assíncrona + webhook
- [ ] Certificado A3 / HSM
- [ ] DANFE PDF multiplataforma
- [ ] CT-e ICMS configurável; MDF-e modais 02–04
- [ ] Homologação NFS-e Nacional (ADN) com evidências
- [ ] ISSQN na **NF-e** (grupo item — distinto deste módulo NFS-e)
- [ ] Importar tabela IBPT vigente da UF enquanto a API De Olho no Imposto estiver intermitente

### Qualidade

- [ ] Cobertura ≥ 80% reportada no CI
- [ ] Golden files XML + validação XSD no CI
- [ ] Testes E2E homologação `[Explicit]`
- [ ] Coleção Postman/Insomnia exportada

---

## Homologação — evidências (preencher após smoke)

| Data | Operador | CRT | cStat | Chave (44) | Protocolo | Observação |
|------|----------|-----|-------|------------|-----------|------------|
| | | 1 Simples | | | | |
| | | 2 Excesso | | | | |
| | | 3 LP/LR | | | | |

Comando: `.\scripts\smoke-minimo.ps1` — guia [`docs/HOMOLOGACAO-RAPIDA.md`](docs/HOMOLOGACAO-RAPIDA.md)

---

## Próximo passo

1. Preencher tabela acima após `smoke-minimo.ps1` com certificado real.  
2. CRT 3: `smoke-homologacao.ps1 -EmitirTodosRegimes`.  
3. Priorizar async/webhook ou DANFE conforme negócio.

---

## Indicadores

| Indicador | Valor | Meta |
|-----------|-------|------|
| Endpoints REST | 30+ | — |
| Testes unitários | 134+ | ≥ 80% cobertura |
| Zeus.Net pin | 2026.8.18.2047 | Versão fixa |
| OpenAC NFSe pin | 1.5.0 | Versão fixa |
