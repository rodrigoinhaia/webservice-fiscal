# PROGRESS — FiscalService

## Status Geral

🟢 **MVP tributário NF-e/NFC-e** — cadastro de emitentes, tributação ampliada, contingência, DF-e, retry SEFAZ, documentação alinhada.

🔴 **Homologação SEFAZ real** — aguarda certificado A1 de teste e execução do smoke com evidências.

---

## Fase Atual

**Operacional + qualidade:** homologação end-to-end, cobertura de testes, DANFE PDF Linux.

---

## Métricas

| Métrica | Status | Detalhe |
|---------|--------|---------|
| Build local | 🟢 | `dotnet build` Release |
| Testes unitários | 🟢 | **125+** testes (xUnit) |
| Testes de integração | 🟡 | Testcontainers — EF 8.0.27 alinhado |
| CI (GitHub Actions) | 🟢 | build + test + docker |
| Homologação NF-e | 🔴 | Checklist pronto; execução pendente |
| DANFE PDF Linux | 🔴 | HTML disponível |

---

## Entregas recentes (2026-05)

- [x] Cadastro de emitentes + `emitenteCnpj` + health de certificados
- [x] ICMS CRT 3 (CST 00–90), Simples (CSOSN), IPI, PIS/COFINS (incl. CST 03), DIFAL
- [x] `NFeTotaisCalculator` — FCP, ST, DIFAL nos totais + validação bruto × qtd
- [x] Contingência SVC-AN/RS/Offline, `SefazRetry`, distribuição DF-e, manifestação
- [x] Pin `Zeus.Net.*` `2026.5.13.1248`, `docs/SCHEMAS-DFE.md`, `docs/GUIA-REGIMES.md`
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
- [ ] ISSQN, II, exportação, grupos especiais (combustível etc.)

### Qualidade

- [ ] Cobertura ≥ 80% reportada no CI
- [ ] Golden files XML + validação XSD no CI
- [ ] Testes E2E homologação `[Explicit]`
- [ ] Coleção Postman/Insomnia exportada

---

## Próximo passo

1. Provisionar certificado e rodar smoke em homologação.  
2. Registrar em `PROGRESS.md` chaves/protocolos por CRT.  
3. Priorizar async/webhook ou DANFE conforme negócio.

---

## Indicadores

| Indicador | Valor | Meta |
|-----------|-------|------|
| Endpoints REST | 26+ | — |
| Testes unitários | 125+ | ≥ 80% cobertura |
| Zeus.Net pin | 2026.5.13.1248 | Versão fixa |
