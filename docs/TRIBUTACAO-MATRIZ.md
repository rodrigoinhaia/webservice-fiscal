# Matriz CST / CSOSN × DFe.NET

Referência de mapeamento do `FiscalService.Api` para classes do **[ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET)** (`NFe.Classes.Informacoes.Detalhe.Tributacao`).

Implementação: `src/FiscalService.Api/Services/Fiscal/ImpostoIcmsMapper.cs`, `ImpostoItemFactory.cs`, `ImpostoTributacaoCatalog.cs`.

## CRT do emitente (`configuracaoEmitente.crt`)

| CRT | Regime | Campo por item |
|-----|--------|----------------|
| 1 | Simples Nacional | `csosnIcms` |
| 2 | Simples (excesso sublimite) | `csosnIcms` |
| 3 | Normal (Lucro Presumido **e** Lucro Real) | `cstIcms` |

> Lucro Presumido e Lucro Real usam o **mesmo CRT 3** na NF-e. O ERP define CST e valores; a API monta o XML.

## ICMS — Regime normal (CRT 3)

| CST | Classe DFe.NET | Status |
|-----|----------------|--------|
| 00 | `ICMS00` | Suportado (default se omitido) |
| 10 | `ICMS10` | Suportado |
| 20 | `ICMS20` | Suportado |
| 30 | `ICMS30` | Suportado |
| 40 | `ICMS40` | Suportado |
| 41 | `ICMS40` | Suportado |
| 50 | `ICMS40` | Suportado |
| 51 | `ICMS51` | Suportado |
| 60 | `ICMS60` | Suportado |
| 70 | `ICMS70` | Suportado |
| 90 | `ICMS90` | Suportado |
| Outros | — | **Rejeitado** (400) — sem fallback silencioso |

## ICMS — Simples Nacional (CRT 1/2)

| CSOSN | Classe DFe.NET | Status |
|-------|----------------|--------|
| 101 | `ICMSSN101` | Suportado |
| 102 | `ICMSSN102` | Suportado (default se omitido) |
| 103 | `ICMSSN201` | Suportado (layout Zeus) |
| 201 | `ICMSSN201` | Suportado |
| 202 | `ICMSSN202` | Suportado |
| 203 | `ICMSSN202` | Suportado |
| 500 | `ICMSSN500` | Suportado |
| 900 | `ICMSSN900` | Suportado |
| Outros | — | **Rejeitado** (400) |

## IPI (opcional por item)

| CST IPI | Classe DFe.NET |
|---------|----------------|
| 00, 49, 50, 99 | `IPITrib` |
| 01–05, 51–55 | `IPINT` |

## PIS / COFINS

| Tipo | Classe | Status |
|------|--------|--------|
| Alíquota | `PISAliq` / `COFINSAliq` | Suportado (default CST 07) |
| NT / Qtde / Outros | — | Roadmap fase 3 |

## DIFAL (partilha ICMS — CRT 3)

Quando o ERP informar `baseCalculoUfDest` no item, o API monta `ICMSUFDest` e totaliza `vICMSUFDest` / `vICMSUFRemet` em `ICMSTot`.

Exemplo: [`exemplos/nfe/crt3-interestadual-difal.json`](exemplos/nfe/crt3-interestadual-difal.json).

## Pendente (roadmap)

- FCP explícito nos totais (além do DIFAL básico)
- ISSQN, II importação
- PIS/COFINS NT, monofásico

Ver [`ROADMAP-TRIBUTACAO-REGIMES.md`](ROADMAP-TRIBUTACAO-REGIMES.md).
